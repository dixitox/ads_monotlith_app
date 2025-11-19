using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailMonolith.Checkout.Api.Data;
using RetailMonolith.Checkout.Api.Services;

namespace RetailMonolith.Checkout.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CheckoutController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPaymentGateway _payments;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(AppDbContext db, IPaymentGateway payments, ILogger<CheckoutController> logger)
    {
        _db = db;
        _payments = payments;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CheckoutAsync([FromBody] CheckoutRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.CustomerId))
                return BadRequest(new { error = "Customer ID is required" });

            if (string.IsNullOrWhiteSpace(request.PaymentToken))
                return BadRequest(new { error = "Payment token is required" });

            // 1) Pull cart
            var cart = await _db.Carts
                .Include(c => c.Lines)
                .FirstOrDefaultAsync(c => c.CustomerId == request.CustomerId, cancellationToken);

            if (cart == null || !cart.Lines.Any())
                return BadRequest(new { error = "Cart not found or empty" });

            var total = cart.Lines.Sum(l => l.UnitPrice * l.Quantity);

            // 2) Reserve/decrement stock (optimistic)
            foreach (var line in cart.Lines)
            {
                var inv = await _db.Inventory.SingleOrDefaultAsync(i => i.Sku == line.Sku, cancellationToken);
                if (inv == null || inv.Quantity < line.Quantity)
                    return BadRequest(new { error = $"Insufficient stock for SKU: {line.Sku}" });
                
                inv.Quantity -= line.Quantity;
            }

            // 3) Charge payment
            var paymentRequest = new PaymentRequest(total, "GBP", request.PaymentToken);
            var paymentResult = await _payments.ChargeAsync(paymentRequest, cancellationToken);
            var status = paymentResult.Succeeded ? "Paid" : "Failed";

            // 4) Create order
            var order = new Models.Order 
            { 
                CustomerId = request.CustomerId, 
                Status = status, 
                Total = total 
            };
            order.Lines = cart.Lines.Select(l => new Models.OrderLine
            {
                Sku = l.Sku,
                Name = l.Name,
                UnitPrice = l.UnitPrice,
                Quantity = l.Quantity
            }).ToList();

            _db.Orders.Add(order);

            // 5) Clear cart
            _db.CartLines.RemoveRange(cart.Lines);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Checkout completed for customer {CustomerId}, Order {OrderId}, Status {Status}", 
                request.CustomerId, order.Id, status);

            return Ok(new CheckoutResponseDto
            {
                OrderId = order.Id,
                Status = status,
                Total = total,
                CreatedUtc = order.CreatedUtc
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during checkout for customer {CustomerId}", request.CustomerId);
            return StatusCode(503, new { error = "Service temporarily unavailable" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during checkout for customer {CustomerId}", request.CustomerId);
            return StatusCode(500, new { error = "An error occurred processing your request" });
        }
    }
}

public sealed class CheckoutRequestDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string PaymentToken { get; set; } = string.Empty;
}

public sealed class CheckoutResponseDto
{
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime CreatedUtc { get; set; }
}

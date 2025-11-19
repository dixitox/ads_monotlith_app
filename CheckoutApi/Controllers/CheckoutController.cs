using Microsoft.AspNetCore.Mvc;
using CheckoutApi.DTOs;
using CheckoutApi.Services;

namespace CheckoutApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CheckoutController : ControllerBase
    {
        private readonly ICheckoutService _checkoutService;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(ICheckoutService checkoutService, ILogger<CheckoutController> logger)
        {
            _checkoutService = checkoutService;
            _logger = logger;
        }

        [HttpPost]
        [ProducesResponseType(typeof(CheckoutResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CheckoutResponse>> Checkout([FromBody] CheckoutRequest request)
        {
            try
            {
                _logger.LogInformation("Processing checkout for customer: {CustomerId}", request.CustomerId);

                var order = await _checkoutService.CheckoutAsync(request.CustomerId, request.PaymentToken);

                var response = new CheckoutResponse
                {
                    OrderId = order.Id,
                    Status = order.Status,
                    Total = order.Total,
                    CreatedUtc = order.CreatedUtc
                };

                _logger.LogInformation("Checkout completed successfully. OrderId: {OrderId}", order.Id);

                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Checkout failed for customer: {CustomerId}", request.CustomerId);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during checkout for customer: {CustomerId}", request.CustomerId);
                return StatusCode(500, new { error = "An error occurred during checkout" });
            }
        }
    }
}

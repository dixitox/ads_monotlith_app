using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RetailMonolith.Checkout.Api;
using RetailMonolith.Checkout.Api.Data;
using RetailMonolith.Checkout.Api.Models;
using Xunit;

namespace RetailMonolith.Checkout.Tests;

public class CheckoutIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CheckoutIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Checkout_FullFlow_CreatesOrderAndDecrementsInventory()
    {
        // Arrange: Set up full checkout scenario with cart and inventory
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                db.Database.EnsureCreated();
                
                // Seed cart with 2 items
                var cart = new Cart { CustomerId = "integration-customer" };
                cart.Lines.Add(new CartLine 
                { 
                    Sku = "INTEG-001", 
                    Name = "Integration Item A", 
                    UnitPrice = 29.99m, 
                    Quantity = 2 
                });
                cart.Lines.Add(new CartLine 
                { 
                    Sku = "INTEG-002", 
                    Name = "Integration Item B", 
                    UnitPrice = 15.00m, 
                    Quantity = 1 
                });
                db.Carts.Add(cart);
                
                // Seed inventory
                db.Inventory.Add(new InventoryItem { Sku = "INTEG-001", Quantity = 100 });
                db.Inventory.Add(new InventoryItem { Sku = "INTEG-002", Quantity = 50 });
                db.SaveChanges();
            });
        }).CreateClient();

        var request = new
        {
            customerId = "integration-customer",
            paymentToken = "tok_integration_test"
        };

        // Act: Perform checkout
        var response = await client.PostAsJsonAsync("/api/checkout", request);

        // Assert: Verify response
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<CheckoutResponse>();
        Assert.NotNull(result);
        Assert.Equal("Paid", result.Status);
        Assert.Equal(74.98m, result.Total); // (29.99 * 2) + (15.00 * 1) = 59.98 + 15.00 = 74.98
        Assert.True(result.OrderId > 0);

        // Verify database state changes by creating a new scope
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Verify order was created
        var order = await verifyDb.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == result.OrderId);
        
        Assert.NotNull(order);
        Assert.Equal("integration-customer", order.CustomerId);
        Assert.Equal("Paid", order.Status);
        Assert.Equal(74.98m, order.Total);
        Assert.Equal(2, order.Lines.Count);
        
        // Verify cart lines were cleared (cart entity may remain but should be empty)
        var remainingCart = await verifyDb.Carts
            .Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.CustomerId == "integration-customer");
        
        // Cart entity may exist but should have no lines
        if (remainingCart != null)
        {
            Assert.Empty(remainingCart.Lines);
        }
        
        // Verify inventory was decremented
        var inventory1 = await verifyDb.Inventory.FirstOrDefaultAsync(i => i.Sku == "INTEG-001");
        var inventory2 = await verifyDb.Inventory.FirstOrDefaultAsync(i => i.Sku == "INTEG-002");
        
        Assert.NotNull(inventory1);
        Assert.Equal(98, inventory1.Quantity); // 100 - 2
        
        Assert.NotNull(inventory2);
        Assert.Equal(49, inventory2.Quantity); // 50 - 1
    }

    private class CheckoutResponse
    {
        public int OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}

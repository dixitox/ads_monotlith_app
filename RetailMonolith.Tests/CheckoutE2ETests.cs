using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RetailMonolith;
using RetailMonolith.Data;
using RetailMonolith.Models;
using RetailMonolith.Services;
using Xunit;

namespace RetailMonolith.Tests;

// Custom factory that properly sets environment before app builds
public class TestingWebApplicationFactory : WebApplicationFactory<ProgramEntry>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Add AppDbContext with InMemory database for testing
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("MonolithE2ETest");
            });

            // Mock the HttpClient for CheckoutService to return a successful order
            // This simulates the API being available
            var mockHttpMessageHandler = new MockHttpMessageHandler((request, cancellationToken) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        id = 999,
                        customerId = "guest",
                        total = 39.98m,
                        currency = "USD",
                        status = "Confirmed",
                        lines = new[] 
                        {
                            new { sku = "E2E-001", quantity = 2, unitPrice = 19.99m }
                        }
                    })
                };
                return Task.FromResult(response);
            });

            // Replace HttpClient registration for CheckoutService
            services.AddHttpClient<ICheckoutService, CheckoutService>(client =>
            {
                client.BaseAddress = new Uri("http://localhost:5100");
            })
            .ConfigurePrimaryHttpMessageHandler(() => mockHttpMessageHandler);
        });
    }
}

// Helper class for mocking HttpMessageHandler
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handler(request, cancellationToken);
    }
}

public class CheckoutE2ETests : IClassFixture<TestingWebApplicationFactory>
{
    private readonly WebApplicationFactory<ProgramEntry> _factory;

    public CheckoutE2ETests(TestingWebApplicationFactory factory)
    {
        _factory = factory;

        // Seed test data after factory is configured
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Database.EnsureCreated();

        // Seed a simple product and cart for the UI flow
        var product = new Product
        {
            Sku = "E2E-001",
            Name = "E2E Test Product",
            Price = 19.99m,
            Currency = "USD",
            Category = "Test"
        };
        db.Products.Add(product);

        db.Inventory.Add(new InventoryItem
        {
            Sku = "E2E-001",
            Quantity = 10
        });

        var cart = new Cart
        {
            CustomerId = "e2e-customer"
        };
        db.Carts.Add(cart);

        db.CartLines.Add(new CartLine
        {
            Cart = cart,
            Sku = "E2E-001",
            Name = "E2E Test Product",
            UnitPrice = 19.99m,
            Quantity = 2
        });

        db.SaveChanges();
    }

    [Fact]
    public async Task Checkout_FullFlow_UsingMonolithUI_CreatesOrder()
    {
        // Arrange: create client (antiforgery disabled in Testing environment)
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act: POST to checkout (no antiforgery token needed in Testing environment)
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("PaymentToken", "tok_e2e_test")
        });

        var response = await client.PostAsync("/Checkout", formContent);

        // Assert: we expect a redirect to Orders/Details
        Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.SeeOther or HttpStatusCode.Found,
            $"Expected redirect, got {response.StatusCode}");
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        Assert.Contains("/Orders/Details", location);

        // Follow redirect and ensure page loads
        var followResponse = await client.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, followResponse.StatusCode);
    }
}

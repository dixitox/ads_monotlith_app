using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RetailDecomposed.Tests;

/// <summary>
/// Functional tests for the Checkout API endpoint in the decomposed application.
/// Tests the checkout process including payment, order creation, and error handling.
/// </summary>
public class CheckoutApiTests : IClassFixture<DecomposedWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CheckoutApiTests(DecomposedWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Checkout_WithValidCart_Returns_CreatedOrder()
    {
        // Arrange - Add items to cart
        var customerId = "checkouttest1";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=2", null);
        var checkoutRequest = new { CustomerId = customerId, PaymentToken = "tok_test" };

        // Act
        var response = await client.PostAsJsonAsync("/api/checkout", checkoutRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal(customerId, order.CustomerId);
        Assert.Equal("Paid", order.Status);
        Assert.True(order.Total > 0);
        Assert.NotEmpty(order.Lines);
    }

    [Fact]
    public async Task Checkout_CreatesOrderWithCorrectTotal()
    {
        // Arrange
        var customerId = "checkouttest2";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=2", null);
        var checkoutRequest = new { CustomerId = customerId, PaymentToken = "tok_test" };

        // Act
        var response = await client.PostAsJsonAsync("/api/checkout", checkoutRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        
        // Product 1 has price 10.99, quantity 2 = 21.98
        Assert.Equal(21.98m, order.Total);
    }

    [Fact]
    public async Task Checkout_ClearsCartAfterSuccess()
    {
        // Arrange
        var customerId = "checkouttest3";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=2", null);
        var checkoutRequest = new { CustomerId = customerId, PaymentToken = "tok_test" };

        // Act
        await client.PostAsJsonAsync("/api/checkout", checkoutRequest);
        
        // Check cart is empty
        var cartResponse = await client.GetAsync($"/api/cart/{customerId}");
        var cart = await cartResponse.Content.ReadFromJsonAsync<CartDto>();

        // Assert
        Assert.NotNull(cart);
        Assert.Empty(cart.Lines);
    }

    [Fact]
    public async Task Checkout_WithEmptyCart_Returns_BadRequest()
    {
        // Arrange - Customer with no cart items
        var client = _client.AuthenticateAsCustomer();
        var customerId = "emptycarttest";
        var checkoutRequest = new { CustomerId = customerId, PaymentToken = "tok_test" };

        // Act
        var response = await client.PostAsJsonAsync("/api/checkout", checkoutRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Contains("not found", error.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Checkout_WithMultipleItems_CreatesOrderWithAllLines()
    {
        // Arrange
        var customerId = "checkouttest4";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=1", null);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=2&quantity=2", null);
        var checkoutRequest = new { CustomerId = customerId, PaymentToken = "tok_test" };

        // Act
        var response = await client.PostAsJsonAsync("/api/checkout", checkoutRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal(2, order.Lines.Count);
        
        // Verify total: Product 1 (10.99 * 1) + Product 2 (20.99 * 2) = 52.97
        Assert.Equal(52.97m, order.Total);
    }

    [Fact]
    public async Task Checkout_CreatesOrderVisibleInOrdersList()
    {
        // Arrange
        var customerId = "checkouttest5";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=1", null);
        var checkoutRequest = new { CustomerId = customerId, PaymentToken = "tok_test" };

        // Act
        var checkoutResponse = await client.PostAsJsonAsync("/api/checkout", checkoutRequest);
        var createdOrder = await checkoutResponse.Content.ReadFromJsonAsync<OrderDto>();
        
        // Get orders list as admin
        var adminClient = _client.AuthenticateAsAdmin();
        var ordersResponse = await adminClient.GetAsync("/api/orders");
        var orders = await ordersResponse.Content.ReadFromJsonAsync<List<OrderDto>>();

        // Assert
        Assert.NotNull(createdOrder);
        Assert.NotNull(orders);
        Assert.Contains(orders, o => o.Id == createdOrder.Id);
    }

    [Fact]
    public async Task Checkout_WithValidPaymentToken_SetsStatusToPaid()
    {
        // Arrange
        var customerId = "checkouttest6";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=1", null);
        var checkoutRequest = new { CustomerId = customerId, PaymentToken = "tok_test" };

        // Act
        var response = await client.PostAsJsonAsync("/api/checkout", checkoutRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal("Paid", order.Status);
    }

    [Fact]
    public async Task Checkout_Returns_OrderWithCorrectLineDetails()
    {
        // Arrange
        var customerId = "checkouttest7";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=2", null);
        var checkoutRequest = new { CustomerId = customerId, PaymentToken = "tok_test" };

        // Act
        var response = await client.PostAsJsonAsync("/api/checkout", checkoutRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        
        var line = order.Lines.First();
        Assert.Equal("TEST-001", line.Sku);
        Assert.Equal("Test Product 1", line.Name);
        Assert.Equal(10.99m, line.UnitPrice);
        Assert.Equal(2, line.Quantity);
    }

    // DTO classes for deserialization
    private class OrderDto
    {
        public int Id { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public DateTime CreatedUtc { get; set; }
        public List<OrderLineDto> Lines { get; set; } = new();
    }

    private class OrderLineDto
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
    }

    private class CartDto
    {
        public int Id { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public List<CartLineDto> Lines { get; set; } = new();
    }

    private class CartLineDto
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
    }

    private class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
    }
}

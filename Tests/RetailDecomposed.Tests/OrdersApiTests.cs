using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RetailDecomposed.Tests;

/// <summary>
/// Functional tests for the Orders API endpoints in the decomposed application.
/// Tests both API endpoints for retrieving orders.
/// </summary>
public class OrdersApiTests : IClassFixture<DecomposedWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OrdersApiTests(DecomposedWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetOrders_Returns_Success()
    {
        // Act
        var response = await _client.GetAsync("/api/orders");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOrders_Returns_ValidListStructure()
    {
        // Act
        var response = await _client.GetAsync("/api/orders");

        // Assert
        response.EnsureSuccessStatusCode();
        var orders = await response.Content.ReadFromJsonAsync<List<OrderDto>>();
        Assert.NotNull(orders);
        // The list may or may not be empty depending on test execution order
        // Just verify the structure is valid
    }

    [Fact]
    public async Task GetOrders_Returns_OrdersWithLines()
    {
        // Arrange - Create an order by checking out
        await _client.PostAsync("/api/cart/ordertestcustomer/items?productId=1&quantity=2", null);
        var checkoutRequest = new { CustomerId = "ordertestcustomer", PaymentToken = "tok_test" };
        await _client.PostAsJsonAsync("/api/checkout", checkoutRequest);

        // Act
        var response = await _client.GetAsync("/api/orders");

        // Assert
        response.EnsureSuccessStatusCode();
        var orders = await response.Content.ReadFromJsonAsync<List<OrderDto>>();
        Assert.NotNull(orders);
        Assert.NotEmpty(orders);
        
        var order = orders.First();
        Assert.NotNull(order.Lines);
        Assert.NotEmpty(order.Lines);
    }

    [Fact]
    public async Task GetOrders_Returns_OrdersInDescendingOrder()
    {
        // Arrange - Create multiple orders
        await _client.PostAsync("/api/cart/ordertest1/items?productId=1&quantity=1", null);
        var checkout1 = new { CustomerId = "ordertest1", PaymentToken = "tok_test" };
        await _client.PostAsJsonAsync("/api/checkout", checkout1);
        
        await Task.Delay(100); // Ensure different timestamps
        
        await _client.PostAsync("/api/cart/ordertest2/items?productId=2&quantity=1", null);
        var checkout2 = new { CustomerId = "ordertest2", PaymentToken = "tok_test" };
        await _client.PostAsJsonAsync("/api/checkout", checkout2);

        // Act
        var response = await _client.GetAsync("/api/orders");

        // Assert
        response.EnsureSuccessStatusCode();
        var orders = await response.Content.ReadFromJsonAsync<List<OrderDto>>();
        Assert.NotNull(orders);
        Assert.True(orders.Count >= 2);
        
        // Verify orders are in descending order by date
        for (int i = 0; i < orders.Count - 1; i++)
        {
            Assert.True(orders[i].CreatedUtc >= orders[i + 1].CreatedUtc);
        }
    }

    [Fact]
    public async Task GetOrderById_WithValidId_Returns_Order()
    {
        // Arrange - Create an order
        await _client.PostAsync("/api/cart/orderbyidtest/items?productId=1&quantity=1", null);
        var checkoutRequest = new { CustomerId = "orderbyidtest", PaymentToken = "tok_test" };
        var checkoutResponse = await _client.PostAsJsonAsync("/api/checkout", checkoutRequest);
        var createdOrder = await checkoutResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(createdOrder);

        // Act
        var response = await _client.GetAsync($"/api/orders/{createdOrder.Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal(createdOrder.Id, order.Id);
        Assert.NotNull(order.Lines);
        Assert.NotEmpty(order.Lines);
    }

    [Fact]
    public async Task GetOrderById_WithInvalidId_Returns_NotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/orders/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOrderById_Returns_OrderWithCorrectCustomerId()
    {
        // Arrange
        await _client.PostAsync("/api/cart/specificcustomer/items?productId=1&quantity=1", null);
        var checkoutRequest = new { CustomerId = "specificcustomer", PaymentToken = "tok_test" };
        var checkoutResponse = await _client.PostAsJsonAsync("/api/checkout", checkoutRequest);
        var createdOrder = await checkoutResponse.Content.ReadFromJsonAsync<OrderDto>();

        // Act
        var response = await _client.GetAsync($"/api/orders/{createdOrder!.Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal("specificcustomer", order.CustomerId);
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
}

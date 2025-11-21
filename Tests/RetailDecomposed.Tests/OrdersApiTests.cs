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
    private readonly DecomposedWebApplicationFactory _factory;

    public OrdersApiTests(DecomposedWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetOrders_Returns_Success()
    {
        // Arrange
        var client = _client.AuthenticateAsAdmin();

        // Act
        var response = await client.GetAsync("/api/orders");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOrders_Returns_ValidListStructure()
    {
        // Arrange
        var client = _client.AuthenticateAsAdmin();

        // Act
        var response = await client.GetAsync("/api/orders");

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
        var customerId = "ordertestcustomer";
        var customerClient = _client.AuthenticateAs(customerId, customerId, $"{customerId}@example.com");
        await customerClient.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=2", null);
        var checkoutRequest = new { CustomerId = customerId, PaymentToken = "tok_test" };
        var checkoutResponse = await customerClient.PostAsJsonAsync("/api/checkout", checkoutRequest);
        checkoutResponse.EnsureSuccessStatusCode();

        // Act - Get orders as admin
        var adminClient = _client.AuthenticateAsAdmin();
        var response = await adminClient.GetAsync("/api/orders");

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
        // Arrange - Create first order
        var customerId1 = "ordertest1";
        var client1 = _factory.CreateClient();
        client1.AuthenticateAs(customerId1, customerId1, $"{customerId1}@example.com");
        await client1.PostAsync($"/api/cart/{customerId1}/items?productId=1&quantity=1", null);
        var checkout1 = new { CustomerId = customerId1, PaymentToken = "tok_test" };
        var checkoutResponse1 = await client1.PostAsJsonAsync("/api/checkout", checkout1);
        checkoutResponse1.EnsureSuccessStatusCode();
        
        await Task.Delay(100); // Ensure different timestamps
        
        // Create second order with fresh client
        var customerId2 = "ordertest2";
        var client2 = _factory.CreateClient();
        client2.AuthenticateAs(customerId2, customerId2, $"{customerId2}@example.com");
        await client2.PostAsync($"/api/cart/{customerId2}/items?productId=2&quantity=1", null);
        var checkout2 = new { CustomerId = customerId2, PaymentToken = "tok_test" };
        var checkoutResponse2 = await client2.PostAsJsonAsync("/api/checkout", checkout2);
        checkoutResponse2.EnsureSuccessStatusCode();

        // Act - Get orders as admin with fresh client
        var adminClient = _factory.CreateClient();
        adminClient.AuthenticateAsAdmin();
        var response = await adminClient.GetAsync("/api/orders");

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
        var customerId = "orderbyidtest";
        var client = _client.AuthenticateAs(customerId, customerId, $"{customerId}@example.com");
        await client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=1", null);
        var checkoutRequest = new { CustomerId = customerId, PaymentToken = "tok_test" };
        var checkoutResponse = await client.PostAsJsonAsync("/api/checkout", checkoutRequest);
        var createdOrder = await checkoutResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(createdOrder);

        // Act
        var response = await client.GetAsync($"/api/orders/{createdOrder.Id}");

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
        // Arrange
        var client = _client.AuthenticateAsCustomer();

        // Act
        var response = await client.GetAsync("/api/orders/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOrderById_Returns_OrderWithCorrectCustomerId()
    {
        // Arrange
        var customerId = "specificcustomer";
        var client = _client.AuthenticateAs(customerId, customerId, $"{customerId}@example.com");
        await client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=1", null);
        var checkoutRequest = new { CustomerId = customerId, PaymentToken = "tok_test" };
        var checkoutResponse = await client.PostAsJsonAsync("/api/checkout", checkoutRequest);
        var createdOrder = await checkoutResponse.Content.ReadFromJsonAsync<OrderDto>();

        // Act
        var response = await client.GetAsync($"/api/orders/{createdOrder!.Id}");

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

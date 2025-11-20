using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RetailDecomposed.Tests;

/// <summary>
/// Functional tests for the Cart API endpoints in the decomposed application.
/// Tests both API endpoints and ensures circular reference handling works.
/// </summary>
public class CartApiTests : IClassFixture<DecomposedWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CartApiTests(DecomposedWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCart_ForNewCustomer_Returns_EmptyCart()
    {
        // Act
        var response = await _client.GetAsync("/api/cart/testcustomer");

        // Assert
        response.EnsureSuccessStatusCode();
        var cart = await response.Content.ReadFromJsonAsync<CartDto>();
        Assert.NotNull(cart);
        Assert.Equal("testcustomer", cart.CustomerId);
        Assert.Empty(cart.Lines);
    }

    [Fact]
    public async Task AddToCart_AddsItemSuccessfully()
    {
        // Arrange
        var customerId = "testcustomer2";
        var productId = 1;
        var quantity = 2;

        // Act
        var response = await _client.PostAsync(
            $"/api/cart/{customerId}/items?productId={productId}&quantity={quantity}", 
            null);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetCart_AfterAddingItem_Returns_CartWithItem()
    {
        // Arrange
        var customerId = "testcustomer3";
        await _client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=2", null);

        // Act
        var response = await _client.GetAsync($"/api/cart/{customerId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var cart = await response.Content.ReadFromJsonAsync<CartDto>();
        Assert.NotNull(cart);
        Assert.Single(cart.Lines);
        Assert.Equal("TEST-001", cart.Lines[0].Sku);
        Assert.Equal(2, cart.Lines[0].Quantity);
    }

    [Fact]
    public async Task GetCart_WithMultipleItems_Returns_AllItems()
    {
        // Arrange
        var customerId = "testcustomer4";
        await _client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=1", null);
        await _client.PostAsync($"/api/cart/{customerId}/items?productId=2&quantity=3", null);

        // Act
        var response = await _client.GetAsync($"/api/cart/{customerId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var cart = await response.Content.ReadFromJsonAsync<CartDto>();
        Assert.NotNull(cart);
        Assert.Equal(2, cart.Lines.Count);
    }

    [Fact]
    public async Task GetCart_DoesNotContainCircularReferences()
    {
        // Arrange - Add an item to cart
        var customerId = "testcustomer5";
        await _client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=1", null);

        // Act - Get cart should not throw JsonException
        var response = await _client.GetAsync($"/api/cart/{customerId}");

        // Assert - Successful deserialization means no circular reference issue
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
        
        // Verify we can deserialize successfully
        var cart = await response.Content.ReadFromJsonAsync<CartDto>();
        Assert.NotNull(cart);
    }

    [Fact]
    public async Task CartPage_Returns_Success()
    {
        // Act
        var response = await _client.GetAsync("/Cart");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // DTO classes for deserialization
    private class CartDto
    {
        public int Id { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public List<CartLineDto> Lines { get; set; } = new();
    }

    private class CartLineDto
    {
        public int Id { get; set; }
        public int CartId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
    }
}

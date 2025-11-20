using System.Net;
using Xunit;

namespace RetailDecomposed.Tests;

/// <summary>
/// Integration tests for the decomposed application pages.
/// Tests that pages work correctly with API clients.
/// </summary>
public class IntegrationTests : IClassFixture<DecomposedWebApplicationFactory>
{
    private readonly HttpClient _client;

    public IntegrationTests(DecomposedWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HomePage_Returns_Success()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CheckoutPage_Returns_Success()
    {
        // Act
        var response = await _client.GetAsync("/Checkout");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OrdersPage_Returns_Success()
    {
        // Act
        var response = await _client.GetAsync("/Orders");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EndToEnd_AddProductToCart_And_ViewCart()
    {
        // Arrange - Navigate to products page
        var productsResponse = await _client.GetAsync("/Products");
        productsResponse.EnsureSuccessStatusCode();

        // Act 1 - Add product to cart via API
        var addToCartResponse = await _client.PostAsync("/api/cart/guest/items?productId=1&quantity=1", null);
        addToCartResponse.EnsureSuccessStatusCode();

        // Act 2 - View cart page
        var cartResponse = await _client.GetAsync("/Cart");
        
        // Assert
        cartResponse.EnsureSuccessStatusCode();
        var cartContent = await cartResponse.Content.ReadAsStringAsync();
        Assert.Contains("Test Product 1", cartContent);
    }
}

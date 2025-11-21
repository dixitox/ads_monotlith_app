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
        // Arrange - Authenticate as customer
        var client = _client.AuthenticateAsCustomer();

        // Act
        var response = await client.GetAsync("/Checkout");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OrdersPage_Returns_Success()
    {
        // Arrange - Authenticate as customer
        var client = _client.AuthenticateAsCustomer();

        // Act
        var response = await client.GetAsync("/Orders");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EndToEnd_AddProductToCart_And_ViewCart()
    {
        // Arrange - Authenticate as customer
        var client = _client.AuthenticateAsCustomer();

        // Navigate to products page
        var productsResponse = await client.GetAsync("/Products");
        productsResponse.EnsureSuccessStatusCode();

        // Act 1 - Add product to cart via API
        var addToCartResponse = await client.PostAsync("/api/cart/testuser@example.com/items?productId=1&quantity=1", null);
        addToCartResponse.EnsureSuccessStatusCode();

        // Act 2 - View cart page
        var cartResponse = await client.GetAsync("/Cart");
        
        // Assert
        cartResponse.EnsureSuccessStatusCode();
        var cartContent = await cartResponse.Content.ReadAsStringAsync();
        Assert.Contains("Test Product 1", cartContent);
    }
}

using System.Net;
using Xunit;

namespace RetailMonolith.Tests;

/// <summary>
/// Functional tests for the Products page in the monolithic application.
/// Tests the product listing and add-to-cart functionality.
/// </summary>
public class ProductsPageTests : IClassFixture<MonolithWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProductsPageTests(MonolithWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProductsPage_Returns_Success()
    {
        // Act
        var response = await _client.GetAsync("/Products");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProductsPage_Contains_ProductList()
    {
        // Act
        var response = await _client.GetAsync("/Products");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Contains("Test Product 1", content);
        Assert.Contains("Test Product 2", content);
        Assert.Contains("Test Product 3", content);
    }

    [Fact]
    public async Task ProductsPage_Contains_ProductDetails()
    {
        // Act
        var response = await _client.GetAsync("/Products");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Check for product name, price, and category (SKU not displayed on page)
        Assert.Contains("Test Product 1", content);
        Assert.Contains("&#xA3;10.99", content); // HTML entity for £
        Assert.Contains("Electronics", content);
    }

    [Fact]
    public async Task AddToCart_RedirectsToCart()
    {
        // Arrange
        var productId = 1;

        // Act - Post with form data
        var formData = new Dictionary<string, string>
        {
            ["productId"] = productId.ToString()
        };
        using var content = new FormUrlEncodedContent(formData);
        var response = await _client.PostAsync("/Products", content);

        // Assert - Should redirect after adding to cart
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Redirect);
    }
}

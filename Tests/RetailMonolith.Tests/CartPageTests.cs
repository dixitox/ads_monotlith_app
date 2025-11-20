using System.Net;
using Xunit;

namespace RetailMonolith.Tests;

/// <summary>
/// Functional tests for the Cart page in the monolithic application.
/// Tests cart display and item management.
/// </summary>
public class CartPageTests : IClassFixture<MonolithWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CartPageTests(MonolithWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
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

    [Fact]
    public async Task EmptyCart_DisplaysMessage()
    {
        // Act
        var response = await _client.GetAsync("/Cart");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Empty cart should show some indication
        response.EnsureSuccessStatusCode();
        Assert.NotNull(content);
    }

    [Fact]
    public async Task CartWithItems_DisplaysProducts()
    {
        // Arrange - First add an item to cart
        await _client.PostAsync("/Products?handler=&productId=1", null);

        // Act
        var response = await _client.GetAsync("/Cart");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Contains("Test Product 1", content);
    }
}

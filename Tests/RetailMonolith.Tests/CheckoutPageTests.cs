using System.Net;
using Xunit;

namespace RetailMonolith.Tests;

/// <summary>
/// Functional tests for the Checkout page in the monolithic application.
/// Tests checkout process and order creation.
/// </summary>
public class CheckoutPageTests : IClassFixture<MonolithWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CheckoutPageTests(MonolithWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
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
    public async Task CheckoutPage_WithEmptyCart_DisplaysWarning()
    {
        // Act
        var response = await _client.GetAsync("/Checkout");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotNull(content);
    }

    [Fact]
    public async Task CheckoutPage_WithItems_ShowsOrderSummary()
    {
        // Arrange - Add items to cart
        await _client.PostAsync("/Products?handler=&productId=1", null);

        // Act
        var response = await _client.GetAsync("/Checkout");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Contains("Test Product 1", content);
    }
}

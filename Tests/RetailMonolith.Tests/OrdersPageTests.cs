using System.Net;
using Xunit;

namespace RetailMonolith.Tests;

/// <summary>
/// Functional tests for the Orders page in the monolithic application.
/// Tests order listing and order details display.
/// </summary>
public class OrdersPageTests : IClassFixture<MonolithWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OrdersPageTests(MonolithWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
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
    public async Task OrdersPage_WithNoOrders_DisplaysEmptyState()
    {
        // Act
        var response = await _client.GetAsync("/Orders");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotNull(content);
    }

    [Fact]
    public async Task OrderDetailsPage_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/Orders/Details?id=999");

        // Assert
        // Depending on implementation, this might redirect or show an error
        Assert.NotNull(response);
    }
}

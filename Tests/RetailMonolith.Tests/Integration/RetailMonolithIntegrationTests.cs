using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RetailMonolith.Tests.Integration;

/// <summary>
/// Integration tests for RetailMonolith application
/// Run these tests with the application running in Docker Compose
/// </summary>
public class RetailMonolithIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private const string BaseUrl = "http://localhost:5068";

    public RetailMonolithIntegrationTests()
    {
        // Create HTTP client for testing
        _client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
    }

    [Fact(DisplayName = "Health endpoint should return healthy status")]
    [Trait("Category", "Integration")]
    public async Task HealthEndpoint_ShouldReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Healthy");
    }

    [Fact(DisplayName = "Home page should load successfully")]
    [Trait("Category", "Integration")]
    public async Task HomePage_ShouldLoadSuccessfully()
    {
        // Act
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Retail");
    }

    [Fact(DisplayName = "Products page should display products")]
    [Trait("Category", "Integration")]
    public async Task ProductsPage_ShouldDisplayProducts()
    {
        // Act
        var response = await _client.GetAsync("/Products");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Products");
        // Should have at least one product (from seed data)
        content.Should().MatchRegex(@"product|item", "Should contain product listings");
    }

    [Fact(DisplayName = "Cart page should be accessible")]
    [Trait("Category", "Integration")]
    public async Task CartPage_ShouldBeAccessible()
    {
        // Act
        var response = await _client.GetAsync("/Cart");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().MatchRegex(@"cart|shopping", "Should be cart page");
    }

    [Fact(DisplayName = "Orders page should be accessible")]
    [Trait("Category", "Integration")]
    public async Task OrdersPage_ShouldBeAccessible()
    {
        // Act
        var response = await _client.GetAsync("/Orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Checkout page should be accessible")]
    [Trait("Category", "Integration")]
    public async Task CheckoutPage_ShouldBeAccessible()
    {
        // Act
        var response = await _client.GetAsync("/Checkout");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Static files (CSS) should be served")]
    [Trait("Category", "Integration")]
    public async Task StaticFiles_ShouldBeServed()
    {
        // Act
        var response = await _client.GetAsync("/css/site.css");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Contain("css");
    }

    [Theory(DisplayName = "All main pages should return 200 OK")]
    [Trait("Category", "Integration")]
    [InlineData("/")]
    [InlineData("/Products")]
    [InlineData("/Cart")]
    [InlineData("/Orders")]
    [InlineData("/Privacy")]
    public async Task MainPages_ShouldReturn200OK(string path)
    {
        // Act
        var response = await _client.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Application should respond within acceptable time")]
    [Trait("Category", "Integration")]
    [Trait("Category", "Performance")]
    public async Task Application_ShouldRespondQuickly()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await _client.GetAsync("/");
        stopwatch.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, "Response should be under 2 seconds");
    }
}

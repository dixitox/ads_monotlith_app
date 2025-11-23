using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RetailDecomposed.Services;
using Xunit;

namespace RetailDecomposed.Tests;

/// <summary>
/// Integration tests for the Search API endpoints.
/// These tests verify that the search endpoints are properly configured and accessible.
/// Note: These tests don't make actual Azure service calls - they verify endpoint behavior.
/// </summary>
public class SearchApiTests : IClassFixture<DecomposedWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly DecomposedWebApplicationFactory _factory;

    public SearchApiTests(DecomposedWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task GetSearchPage_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/Search");

        // Assert
        Assert.True(
            response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Redirect,
            $"Expected success or redirect, got {response.StatusCode}"
        );
    }

    [Fact]
    public async Task GetCategories_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/search/categories");

        // Assert
        // The endpoint may require authentication or Azure services
        // We're just verifying it exists and is routable
        Assert.True(
            response.IsSuccessStatusCode || 
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"Expected success, unauthorized, or service unavailable, got {response.StatusCode}"
        );
    }

    [Fact]
    public async Task SearchEndpoint_Exists()
    {
        // Act
        var response = await _client.GetAsync("/api/search?query=test");

        // Assert
        // The endpoint should exist (not return 404)
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateIndexEndpoint_Exists()
    {
        // Act
        var response = await _client.PostAsync("/api/search/create-index", null);

        // Assert
        // The endpoint should exist (not return 404)
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task IndexProductsEndpoint_Exists()
    {
        // Act
        var response = await _client.PostAsync("/api/search/index", null);

        // Assert
        // The endpoint should exist (not return 404)
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task Search_WithEmptyQuery_HandlesGracefully(string? query)
    {
        // Act
        var url = string.IsNullOrEmpty(query) 
            ? "/api/search" 
            : $"/api/search?query={Uri.EscapeDataString(query)}";
        
        var response = await _client.GetAsync(url);

        // Assert
        // Should not return server error
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithValidQuery_ReturnsExpectedContentType()
    {
        // Act
        var response = await _client.GetAsync("/api/search?query=test");

        // Assert
        if (response.IsSuccessStatusCode)
        {
            Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString() ?? "");
        }
    }

    [Theory]
    [InlineData("Electronics")]
    [InlineData("Clothing")]
    [InlineData("Outdoor")]
    public async Task Search_WithCategoryFilter_AcceptsParameter(string category)
    {
        // Act
        var response = await _client.GetAsync($"/api/search?query=test&category={Uri.EscapeDataString(category)}");

        // Assert
        // Should accept the parameter without error
        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithTopParameter_AcceptsParameter()
    {
        // Act
        var response = await _client.GetAsync("/api/search?query=test&top=5");

        // Assert
        // Should accept the parameter without error
        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1001)] // Assuming there's a max limit
    public async Task Search_WithInvalidTopParameter_HandlesGracefully(int top)
    {
        // Act
        var response = await _client.GetAsync($"/api/search?query=test&top={top}");

        // Assert
        // Should not crash - either BadRequest or handle gracefully
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest || 
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"Unexpected status code: {response.StatusCode}"
        );
    }

    [Fact]
    public async Task Search_WithLongQuery_HandlesGracefully()
    {
        // Arrange
        var longQuery = new string('a', 1000);

        // Act
        var response = await _client.GetAsync($"/api/search?query={Uri.EscapeDataString(longQuery)}");

        // Assert
        // Should not return server error
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithSpecialCharacters_HandlesGracefully()
    {
        // Arrange
        var specialQuery = "test@#$%^&*()[]{}|\\:;<>?,./";

        // Act
        var response = await _client.GetAsync($"/api/search?query={Uri.EscapeDataString(specialQuery)}");

        // Assert
        // Should not return server error
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithUnicodeCharacters_HandlesGracefully()
    {
        // Arrange
        var unicodeQuery = "test 日本語 العربية Ελληνικά";

        // Act
        var response = await _client.GetAsync($"/api/search?query={Uri.EscapeDataString(unicodeQuery)}");

        // Assert
        // Should not return server error
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task CreateIndex_ReturnsSuccess()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/search/create-index");
        request.Headers.Add("X-Test-UserId", "admin-user-id");
        request.Headers.Add("X-Test-UserName", "Admin User");
        request.Headers.Add("X-Test-UserRoles", "Admin");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        // In test environment with mock service, should return success
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Search index created or updated successfully", content);
    }

    [Fact]
    public async Task IndexProducts_ReturnsSuccess()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/search/index");
        request.Headers.Add("X-Test-UserId", "admin-user-id");
        request.Headers.Add("X-Test-UserName", "Admin User");
        request.Headers.Add("X-Test-UserRoles", "Admin");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        // In test environment with mock service, should return success
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Successfully indexed", content);
        Assert.Contains("products", content);
    }
}

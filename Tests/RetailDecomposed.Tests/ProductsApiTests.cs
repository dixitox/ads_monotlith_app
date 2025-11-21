using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RetailDecomposed.Tests;

/// <summary>
/// Functional tests for the Products API endpoints in the decomposed application.
/// Tests both API endpoints and the Products page.
/// </summary>
public class ProductsApiTests : IClassFixture<DecomposedWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProductsApiTests(DecomposedWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProducts_Returns_SuccessAndProducts()
    {
        // Act
        var response = await _client.GetAsync("/api/products");

        // Assert
        response.EnsureSuccessStatusCode();
        var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();
        Assert.NotNull(products);
        Assert.Equal(3, products.Count);
    }

    [Fact]
    public async Task GetProducts_Returns_ExpectedProducts()
    {
        // Act
        var response = await _client.GetAsync("/api/products");
        var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();

        // Assert
        Assert.NotNull(products);
        Assert.Contains(products, p => p.Name == "Test Product 1");
        Assert.Contains(products, p => p.Name == "Test Product 2");
        Assert.Contains(products, p => p.Name == "Test Product 3");
    }

    [Fact]
    public async Task GetProductById_WithValidId_Returns_Product()
    {
        // Act
        var response = await _client.GetAsync("/api/products/1");

        // Assert
        response.EnsureSuccessStatusCode();
        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(product);
        Assert.Equal("Test Product 1", product.Name);
        Assert.Equal("TEST-001", product.Sku);
    }

    [Fact]
    public async Task GetProductById_WithInvalidId_Returns_NotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/products/999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ProductsPage_Returns_Success()
    {
        // Arrange - Authenticate as customer to access protected page
        var client = _client.AuthenticateAsCustomer();

        // Act
        var response = await client.GetAsync("/Products");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProductsPage_Contains_ProductList()
    {
        // Arrange - Authenticate as customer to access protected page
        var client = _client.AuthenticateAsCustomer();

        // Act
        var response = await client.GetAsync("/Products");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Contains("Test Product 1", content);
        Assert.Contains("Test Product 2", content);
        Assert.Contains("Test Product 3", content);
    }

    // DTO class for deserialization
    private class ProductDto
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? Category { get; set; }
    }
}

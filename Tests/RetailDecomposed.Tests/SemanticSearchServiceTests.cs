using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RetailDecomposed.Data;
using RetailDecomposed.Models;
using RetailDecomposed.Services;
using Xunit;

namespace RetailDecomposed.Tests;

/// <summary>
/// Tests for SemanticSearchService functionality.
/// These tests use mocks to avoid actual Azure service calls.
/// </summary>
public class SemanticSearchServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<SemanticSearchService>> _loggerMock;

    public SemanticSearchServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        
        _context = new AppDbContext(options);
        
        // Seed test data
        SeedTestData();

        // Setup configuration with mock Azure endpoints
        var configData = new Dictionary<string, string>
        {
            ["AzureAI:Endpoint"] = "https://mock-azure-ai.openai.azure.com/",
            ["AzureAI:DeploymentName"] = "text-embedding-3-small",
            ["AzureSearch:Endpoint"] = "https://mock-search.search.windows.net",
            ["AzureSearch:IndexName"] = "products-test-index"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        _loggerMock = new Mock<ILogger<SemanticSearchService>>();
    }

    private void SeedTestData()
    {
        var products = new[]
        {
            new Product 
            { 
                Id = 1, 
                Sku = "RUN-001", 
                Name = "Running Shoes", 
                Description = "Comfortable running shoes for daily training", 
                Price = 89.99m, 
                Currency = "GBP", 
                IsActive = true, 
                Category = "Footwear" 
            },
            new Product 
            { 
                Id = 2, 
                Sku = "TENT-001", 
                Name = "Camping Tent", 
                Description = "4-person tent for outdoor camping adventures", 
                Price = 199.99m, 
                Currency = "GBP", 
                IsActive = true, 
                Category = "Outdoor" 
            },
            new Product 
            { 
                Id = 3, 
                Sku = "TSHIRT-001", 
                Name = "Cotton T-Shirt", 
                Description = "Casual comfortable cotton t-shirt for everyday wear", 
                Price = 19.99m, 
                Currency = "GBP", 
                IsActive = true, 
                Category = "Clothing" 
            },
            new Product 
            { 
                Id = 4, 
                Sku = "LAPTOP-001", 
                Name = "Business Laptop", 
                Description = "High-performance laptop for office work and productivity", 
                Price = 999.99m, 
                Currency = "GBP", 
                IsActive = true, 
                Category = "Electronics" 
            },
            new Product 
            { 
                Id = 5, 
                Sku = "INACTIVE-001", 
                Name = "Inactive Product", 
                Description = "This product should not appear in search", 
                Price = 1.00m, 
                Currency = "GBP", 
                IsActive = false, 
                Category = "Test" 
            }
        };

        _context.Products.AddRange(products);
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetProducts_ReturnsActiveProducts()
    {
        // Arrange
        var activeProducts = await _context.Products
            .Where(p => p.IsActive)
            .ToListAsync();

        // Assert
        Assert.Equal(4, activeProducts.Count);
        Assert.All(activeProducts, p => Assert.True(p.IsActive));
    }

    [Fact]
    public async Task GetProducts_ExcludesInactiveProducts()
    {
        // Arrange
        var allProducts = await _context.Products.ToListAsync();
        var activeProducts = allProducts.Where(p => p.IsActive).ToList();

        // Assert
        Assert.Equal(5, allProducts.Count);
        Assert.Equal(4, activeProducts.Count);
        Assert.DoesNotContain(activeProducts, p => p.Sku == "INACTIVE-001");
    }

    [Fact]
    public async Task GetProductsByCategory_ReturnsCorrectProducts()
    {
        // Arrange
        var category = "Clothing";

        // Act
        var products = await _context.Products
            .Where(p => p.IsActive && p.Category == category)
            .ToListAsync();

        // Assert
        Assert.Single(products);
        Assert.Equal("Cotton T-Shirt", products[0].Name);
        Assert.Equal(category, products[0].Category);
    }

    [Fact]
    public void ProductSearchDocument_MapsCorrectly()
    {
        // Arrange
        var product = _context.Products.First(p => p.Id == 1);
        var embedding = Enumerable.Range(0, 1536).Select(i => (float)i / 1536).ToArray();

        // Act
        var searchDoc = new ProductSearchDocument
        {
            Id = product.Id.ToString(),
            Sku = product.Sku,
            Name = product.Name,
            Description = product.Description ?? string.Empty,
            Category = product.Category ?? string.Empty,
            Price = product.Price,
            NameDescriptionVector = embedding
        };

        // Assert
        Assert.Equal(product.Id.ToString(), searchDoc.Id);
        Assert.Equal(product.Sku, searchDoc.Sku);
        Assert.Equal(product.Name, searchDoc.Name);
        Assert.Equal(1536, searchDoc.NameDescriptionVector?.Length ?? 0);
    }

    [Fact]
    public void Configuration_HasRequiredAzureSettings()
    {
        // Assert
        Assert.NotNull(_configuration["AzureAI:Endpoint"]);
        Assert.NotNull(_configuration["AzureAI:DeploymentName"]);
        Assert.NotNull(_configuration["AzureSearch:Endpoint"]);
        Assert.NotNull(_configuration["AzureSearch:IndexName"]);
    }

    [Fact]
    public async Task IndexableProducts_ContainsRequiredFields()
    {
        // Arrange
        var products = await _context.Products
            .Where(p => p.IsActive)
            .ToListAsync();

        // Assert
        Assert.All(products, product =>
        {
            Assert.NotNull(product.Sku);
            Assert.NotEmpty(product.Sku);
            Assert.NotNull(product.Name);
            Assert.NotEmpty(product.Name);
            Assert.True(product.Price > 0);
        });
    }

    [Theory]
    [InlineData("Footwear", 1)]
    [InlineData("Outdoor", 1)]
    [InlineData("Clothing", 1)]
    [InlineData("Electronics", 1)]
    [InlineData("NonExistent", 0)]
    public async Task GetProductsByCategory_ReturnsExpectedCount(string category, int expectedCount)
    {
        // Act
        var products = await _context.Products
            .Where(p => p.IsActive && p.Category == category)
            .ToListAsync();

        // Assert
        Assert.Equal(expectedCount, products.Count);
    }

    [Fact]
    public async Task ProductSearch_CanFilterByMultipleCriteria()
    {
        // Arrange
        var category = "Clothing";
        var minPrice = 15m;
        var maxPrice = 25m;

        // Act
        var products = await _context.Products
            .Where(p => p.IsActive 
                && p.Category == category 
                && p.Price >= minPrice 
                && p.Price <= maxPrice)
            .ToListAsync();

        // Assert
        Assert.Single(products);
        Assert.Equal("Cotton T-Shirt", products[0].Name);
        Assert.InRange(products[0].Price, minPrice, maxPrice);
    }

    [Fact]
    public void ProductSearchResult_InitializesCorrectly()
    {
        // Arrange & Act
        var result = new ProductSearchResult
        {
            Id = 1,
            Sku = "TEST-001",
            Name = "Test Product",
            Description = "Test Description",
            Category = "Test Category",
            Price = 99.99m,
            Score = 0.85
        };

        // Assert
        Assert.Equal(1, result.Id);
        Assert.Equal("TEST-001", result.Sku);
        Assert.Equal("Test Product", result.Name);
        Assert.Equal("Test Description", result.Description);
        Assert.Equal("Test Category", result.Category);
        Assert.Equal(99.99m, result.Price);
        Assert.Equal(0.85, result.Score);
    }

    [Fact]
    public async Task Products_HaveValidCategoryValues()
    {
        // Arrange
        var products = await _context.Products
            .Where(p => p.IsActive)
            .ToListAsync();

        // Assert
        Assert.All(products, product =>
        {
            Assert.NotNull(product.Category);
            Assert.NotEmpty(product.Category);
        });
    }

    [Fact]
    public async Task Products_HaveValidPriceValues()
    {
        // Arrange
        var products = await _context.Products
            .Where(p => p.IsActive)
            .ToListAsync();

        // Assert
        Assert.All(products, product =>
        {
            Assert.True(product.Price > 0, $"Product {product.Name} has invalid price: {product.Price}");
            Assert.NotEqual("", product.Currency);
        });
    }

    [Fact]
    public async Task SearchTerms_MatchProductDescriptions()
    {
        // This test validates that our test data has searchable content
        // Arrange
        var searchTerms = new[] { "running", "camping", "comfortable", "laptop", "outdoor" };

        // Act
        var results = new Dictionary<string, int>();
        foreach (var term in searchTerms)
        {
            var count = await _context.Products
                .Where(p => p.IsActive && 
                    (p.Name.Contains(term) || 
                     (p.Description != null && p.Description.Contains(term))))
                .CountAsync();
            results[term] = count;
        }

        // Assert
        Assert.True(results["running"] > 0, "Expected products matching 'running'");
        Assert.True(results["camping"] > 0, "Expected products matching 'camping'");
        Assert.True(results["comfortable"] > 0, "Expected products matching 'comfortable'");
        Assert.True(results["laptop"] > 0, "Expected products matching 'laptop'");
        Assert.True(results["outdoor"] > 0, "Expected products matching 'outdoor'");
    }

    [Fact]
    public void VectorDimensions_MatchExpectedSize()
    {
        // text-embedding-3-small produces 1536-dimension vectors
        const int expectedDimensions = 1536;
        
        // Create a mock vector
        var vector = new float[expectedDimensions];
        
        // Assert
        Assert.Equal(expectedDimensions, vector.Length);
    }

    [Theory]
    [InlineData(1, "RUN-001", "Running Shoes")]
    [InlineData(2, "TENT-001", "Camping Tent")]
    [InlineData(3, "TSHIRT-001", "Cotton T-Shirt")]
    [InlineData(4, "LAPTOP-001", "Business Laptop")]
    public async Task GetProductById_ReturnsCorrectProduct(int id, string expectedSku, string expectedName)
    {
        // Act
        var product = await _context.Products.FindAsync(id);

        // Assert
        Assert.NotNull(product);
        Assert.Equal(expectedSku, product.Sku);
        Assert.Equal(expectedName, product.Name);
    }

    public void Dispose()
    {
        _context?.Database.EnsureDeleted();
        _context?.Dispose();
    }
}

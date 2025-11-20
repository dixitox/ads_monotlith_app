using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RetailMonolith.Data;
using RetailMonolith.Models;

namespace RetailDecomposed.Tests;

/// <summary>
/// Custom WebApplicationFactory for testing the RetailDecomposed application.
/// Uses in-memory database to isolate tests from the real database.
/// </summary>
public class DecomposedWebApplicationFactory : WebApplicationFactory<RetailDecomposed.Program>
{
    private readonly string _databaseName = $"TestDb_{Guid.NewGuid()}";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Set environment to Testing to trigger environment-based database configuration
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registrations
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            // Add in-memory database for testing with unique database name
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            // Replace HttpClient factory configurations to use test server
            // Remove the existing HTTP client registrations that point to external URLs
            var productsClientDescriptor = services.FirstOrDefault(d => 
                d.ServiceType == typeof(RetailDecomposed.Services.IProductsApiClient));
            if (productsClientDescriptor != null)
                services.Remove(productsClientDescriptor);

            var cartClientDescriptor = services.FirstOrDefault(d => 
                d.ServiceType == typeof(RetailDecomposed.Services.ICartApiClient));
            if (cartClientDescriptor != null)
                services.Remove(cartClientDescriptor);

            // Build the service provider
            var sp = services.BuildServiceProvider();

            // Create a scope to obtain a reference to the database context
            using var scope = sp.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<AppDbContext>();

            // Ensure the database is created
            db.Database.EnsureCreated();

            // Seed test data
            SeedTestData(db);
        });

        return base.CreateHost(builder);
    }

    /// <summary>
    /// Override to configure services that need the test server's HTTP client
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Add HTTP client for ProductsApiClient that uses test server
            services.AddScoped<RetailDecomposed.Services.IProductsApiClient>(sp =>
            {
                var client = CreateClient();
                return new RetailDecomposed.Services.ProductsApiClient(client);
            });

            // Add HTTP client for CartApiClient that uses test server
            services.AddScoped<RetailDecomposed.Services.ICartApiClient>(sp =>
            {
                var client = CreateClient();
                return new RetailDecomposed.Services.CartApiClient(client);
            });
        });

        base.ConfigureWebHost(builder);
    }

    private void SeedTestData(AppDbContext db)
    {
        // Only seed if no products exist
        if (db.Products.Any())
            return;

        // Add test products
        var products = new[]
        {
            new Product { Id = 1, Sku = "TEST-001", Name = "Test Product 1", Description = "Test Description 1", Price = 10.99m, Currency = "GBP", IsActive = true, Category = "Electronics" },
            new Product { Id = 2, Sku = "TEST-002", Name = "Test Product 2", Description = "Test Description 2", Price = 20.99m, Currency = "GBP", IsActive = true, Category = "Apparel" },
            new Product { Id = 3, Sku = "TEST-003", Name = "Test Product 3", Description = "Test Description 3", Price = 30.99m, Currency = "GBP", IsActive = true, Category = "Accessories" }
        };

        db.Products.AddRange(products);
        db.SaveChanges();
    }
}

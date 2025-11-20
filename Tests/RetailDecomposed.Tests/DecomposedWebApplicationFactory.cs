using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("InMemoryDecomposedTestDb");
            });

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

    private void SeedTestData(AppDbContext db)
    {
        // Clear existing data
        db.Products.RemoveRange(db.Products);
        db.Carts.RemoveRange(db.Carts);
        db.Orders.RemoveRange(db.Orders);
        db.SaveChanges();

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

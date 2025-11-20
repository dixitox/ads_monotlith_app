using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RetailMonolith.Data;
using RetailMonolith.Models;

namespace RetailMonolith.Tests;

/// <summary>
/// Custom WebApplicationFactory for testing the RetailMonolith application.
/// Uses in-memory database to isolate tests from the real database.
/// Each instance uses a unique database name to support parallel test execution.
/// </summary>
public class MonolithWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment to Testing so Program.cs uses in-memory database
        builder.UseEnvironment("Testing");
        
        builder.ConfigureServices(services =>
        {
            // Remove all existing DbContext registrations
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            // Add DbContext with unique in-memory database name
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            // Disable antiforgery validation for testing
            services.AddAntiforgery(options => options.SuppressXFrameOptionsHeader = true);
            services.AddRazorPages().AddRazorPagesOptions(options =>
            {
                options.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryTokenAttribute());
            });

            // Build the service provider to seed the database
            var sp = services.BuildServiceProvider();

            using var scope = sp.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<AppDbContext>();

            // Ensure the database is created and seed test data
            db.Database.EnsureCreated();
            SeedTestData(db);
        });
    }

    private void SeedTestData(AppDbContext db)
    {
        // Only seed if database is empty
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

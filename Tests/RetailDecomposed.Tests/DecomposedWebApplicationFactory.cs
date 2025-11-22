using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
            // Replace authentication with fake authentication for testing
            services.RemoveAll<IAuthenticationSchemeProvider>();
            services.AddAuthentication(FakeAuthenticationHandler.AuthenticationScheme)
                .AddScheme<AuthenticationSchemeOptions, FakeAuthenticationHandler>(
                    FakeAuthenticationHandler.AuthenticationScheme, options => { });

            // Remove existing DbContext registrations
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            // Add in-memory database for testing with unique database name
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
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

    /// <summary>
    /// Override to configure services that need the test server's HTTP client
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Disable antiforgery validation for testing
            services.AddAntiforgery(options => options.SuppressXFrameOptionsHeader = true);
            services.AddRazorPages().AddRazorPagesOptions(options =>
            {
                options.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryTokenAttribute());
            });

            // Remove the existing HTTP client registrations that were added in Program.cs
            var descriptorsToRemove = services.Where(d => 
                d.ServiceType == typeof(RetailDecomposed.Services.IProductsApiClient) ||
                d.ServiceType == typeof(RetailDecomposed.Services.ICartApiClient) ||
                d.ServiceType == typeof(RetailDecomposed.Services.IOrdersApiClient) ||
                d.ServiceType == typeof(RetailDecomposed.Services.ICheckoutApiClient))
                .ToList();
            
            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Register the authentication propagating handler
            services.AddTransient<AuthenticationPropagatingHandler>();

            // Re-register API clients with test server configuration
            // Use ConfigurePrimaryHttpMessageHandler to route through the test server
            services.AddHttpClient<RetailDecomposed.Services.IProductsApiClient, RetailDecomposed.Services.ProductsApiClient>(client =>
            {
                client.BaseAddress = ClientOptions.BaseAddress;
            })
            .ConfigurePrimaryHttpMessageHandler(() => Server.CreateHandler())
            .AddHttpMessageHandler<AuthenticationPropagatingHandler>();

            services.AddHttpClient<RetailDecomposed.Services.ICartApiClient, RetailDecomposed.Services.CartApiClient>(client =>
            {
                client.BaseAddress = ClientOptions.BaseAddress;
            })
            .ConfigurePrimaryHttpMessageHandler(() => Server.CreateHandler())
            .AddHttpMessageHandler<AuthenticationPropagatingHandler>();

            services.AddHttpClient<RetailDecomposed.Services.IOrdersApiClient, RetailDecomposed.Services.OrdersApiClient>(client =>
            {
                client.BaseAddress = ClientOptions.BaseAddress;
            })
            .ConfigurePrimaryHttpMessageHandler(() => Server.CreateHandler())
            .AddHttpMessageHandler<AuthenticationPropagatingHandler>();

            services.AddHttpClient<RetailDecomposed.Services.ICheckoutApiClient, RetailDecomposed.Services.CheckoutApiClient>(client =>
            {
                client.BaseAddress = ClientOptions.BaseAddress;
            })
            .ConfigurePrimaryHttpMessageHandler(() => Server.CreateHandler())
            .AddHttpMessageHandler<AuthenticationPropagatingHandler>();
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

        // Add inventory items for the test products
        var inventoryItems = new[]
        {
            new InventoryItem { Sku = "TEST-001", Quantity = 1000 },
            new InventoryItem { Sku = "TEST-002", Quantity = 1000 },
            new InventoryItem { Sku = "TEST-003", Quantity = 1000 }
        };

        db.Inventory.AddRange(inventoryItems);
        db.SaveChanges();
    }
}

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

        // Configure Azure AD settings for tests with intentionally invalid values
        // 
        // RATIONALE: In Program.cs, the app checks if Azure AD is properly configured by validating
        // that TenantId and ClientId are valid GUIDs. If they are NOT valid GUIDs, it sets
        // isAzureAdConfigured = false. However, when Environment is "Testing", the app still
        // enables authorization (requireAuthorization = isAzureAdConfigured || isTesting).
        //
        // This approach allows us to:
        // 1. Test authorization behavior without needing real Azure AD credentials
        // 2. Use FakeAuthenticationHandler to simulate authenticated users
        // 3. Verify that endpoints properly enforce authorization requirements
        //
        // The values below are intentionally NOT valid GUIDs to prevent accidental real Azure AD calls
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["AzureAd:TenantId"] = "test-tenant-not-a-guid",
                ["AzureAd:ClientId"] = "test-client-not-a-guid",
                ["AzureAd:Domain"] = "test.onmicrosoft.com",
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                // Mock Azure AI configuration to allow CopilotService initialization in tests
                ["AzureAI:Endpoint"] = "https://mock-azure-ai.openai.azure.com/",
                ["AzureAI:DeploymentName"] = "gpt-4o-test",
                ["AzureAI:MaxTokens"] = "800",
                ["AzureAI:Temperature"] = "0.7"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace authentication with fake authentication for testing
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = FakeAuthenticationHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = FakeAuthenticationHandler.AuthenticationScheme;
            })
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
            // Remove Microsoft Identity authentication added by Program.cs
            // We need our FakeAuthenticationHandler to be the only authentication scheme
            var authDescriptors = services.Where(d => 
                d.ServiceType.FullName?.Contains("Microsoft.Identity") == true ||
                d.ServiceType.FullName?.Contains("Microsoft.AspNetCore.Authentication.OpenIdConnect") == true)
                .ToList();
            
            foreach (var descriptor in authDescriptors)
            {
                services.Remove(descriptor);
            }

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

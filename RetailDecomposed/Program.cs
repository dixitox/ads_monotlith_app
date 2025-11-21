using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using RetailMonolith.Data;
using RetailMonolith.Services;
using System.Security.Claims;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Use in-memory database for testing, SQL Server for production
if (builder.Environment.EnvironmentName == "Testing")
{
    builder.Services.AddDbContext<AppDbContext>(o =>
        o.UseInMemoryDatabase("TestDatabase"));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(o =>
        o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ??
                       "Server=(localdb)\\MSSQLLocalDB;Database=RetailMonolith;Trusted_Connection=True;MultipleActiveResultSets=true"));
}

// Configure JSON serialization to handle circular references
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Add Microsoft Entra ID authentication
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(options =>
    {
        builder.Configuration.Bind("AzureAd", options);
        options.SignedOutRedirectUri = "/";
    });

// Add authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));
    options.AddPolicy("CustomerAccess", policy =>
        policy.RequireAuthenticatedUser());
});

builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();

// Add controllers for MicrosoftIdentity UI
builder.Services.AddControllers();

// Add antiforgery services
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

builder.Services.AddScoped<IPaymentGateway, MockPaymentGateway>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<ICartService, CartService>();

// Add support for propagating user tokens to downstream services
builder.Services.AddHttpContextAccessor();

// Register Cart API Client for decomposed Cart module
// Note: Token propagation for API-to-API calls can be added later using custom DelegatingHandler
builder.Services.AddHttpClient<RetailDecomposed.Services.ICartApiClient, RetailDecomposed.Services.CartApiClient>(client =>
{
    var cartApiBaseAddress = builder.Configuration["CartApi:BaseAddress"] ?? "https://localhost:6068";
    client.BaseAddress = new Uri(cartApiBaseAddress);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (builder.Environment.IsDevelopment())
    {
        // Allow untrusted certificates in development only
        handler.ServerCertificateCustomValidationCallback = 
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
});

// Register Products API Client for decomposed Products module
builder.Services.AddHttpClient<RetailDecomposed.Services.IProductsApiClient, RetailDecomposed.Services.ProductsApiClient>(client =>
{
    client.BaseAddress = new Uri("https://localhost:6068");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback = 
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
});

// Register Orders API Client for decomposed Orders module
builder.Services.AddHttpClient<RetailDecomposed.Services.IOrdersApiClient, RetailDecomposed.Services.OrdersApiClient>(client =>
{
    client.BaseAddress = new Uri("https://localhost:6068");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback = 
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
});

// Register Checkout API Client for decomposed Checkout module
builder.Services.AddHttpClient<RetailDecomposed.Services.ICheckoutApiClient, RetailDecomposed.Services.CheckoutApiClient>(client =>
{
    client.BaseAddress = new Uri("https://localhost:6068");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback = 
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
});

builder.Services.AddHealthChecks();

var app = builder.Build();

// Skip migrations and seeding in test environment
if (app.Environment.EnvironmentName != "Testing")
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await AppDbContext.SeedAsync(db);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers(); // Enable MicrosoftIdentity area controllers

// Cart API surface for decomposition
app.MapGet("/api/cart/{customerId}", async (string customerId, ICartService cart, ClaimsPrincipal user) =>
{
    // Validate that the authenticated user matches the customerId
    var authenticatedUserId = user.Identity?.Name;
    if (authenticatedUserId != customerId)
    {
        return Results.Forbid();
    }
    
    var cartData = await cart.GetCartWithLinesAsync(customerId);
    return Results.Ok(cartData);
}).RequireAuthorization("CustomerAccess");

app.MapPost("/api/cart/{customerId}/items", async (string customerId, int productId, int quantity, ICartService cart, ClaimsPrincipal user) =>
{
    // Validate that the authenticated user matches the customerId
    var authenticatedUserId = user.Identity?.Name;
    if (authenticatedUserId != customerId)
    {
        return Results.Forbid();
    }
    
    await cart.AddToCartAsync(customerId, productId, quantity);
    return Results.Ok();
}).RequireAuthorization("CustomerAccess");

// Products API surface for decomposition
app.MapGet("/api/products", async (AppDbContext db) =>
{
    var products = await db.Products.Where(p => p.IsActive).ToListAsync();
    return Results.Ok(products);
}).AllowAnonymous();

app.MapGet("/api/products/{id}", async (int id, AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    if (product is null)
        return Results.NotFound();
    return Results.Ok(product);
}).AllowAnonymous();

// Orders API surface for decomposition
app.MapGet("/api/orders", async (AppDbContext db) =>
{
    var orders = await db.Orders
        .Include(o => o.Lines)
        .OrderByDescending(o => o.CreatedUtc)
        .ToListAsync();
    return Results.Ok(orders);
}).RequireAuthorization("AdminOnly");

app.MapGet("/api/orders/{id}", async (int id, AppDbContext db) =>
{
    var order = await db.Orders
        .Include(o => o.Lines)
        .FirstOrDefaultAsync(o => o.Id == id);
    if (order is null)
        return Results.NotFound();
    return Results.Ok(order);
}).AllowAnonymous();

// Checkout API surface for decomposition
app.MapPost("/api/checkout", async (CheckoutRequest request, ICheckoutService checkoutService) =>
{
    try
    {
        var order = await checkoutService.CheckoutAsync(request.CustomerId, request.PaymentToken);
        return Results.Ok(order);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).AllowAnonymous();

// Display API endpoints banner
var urls = app.Urls.FirstOrDefault() ?? "http://localhost:6068";
Console.WriteLine("\n" + new string('=', 80));
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("  RETAIL DECOMPOSED - Microservices Architecture");
Console.ResetColor();
Console.WriteLine(new string('=', 80));
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"\n  Application running at: {urls}\n");
Console.ResetColor();
Console.WriteLine("  Decomposed API Endpoints:");
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ┌─ Products API");
Console.ResetColor();
Console.WriteLine("  │  ├─ GET  /api/products        → List all active products");
Console.WriteLine("  │  └─ GET  /api/products/{id}   → Get product by ID");
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ┌─ Cart API");
Console.ResetColor();
Console.WriteLine("  │  ├─ GET  /api/cart/{customerId}       → Get customer cart");
Console.WriteLine("  │  └─ POST /api/cart/{customerId}/items → Add item to cart");
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ┌─ Orders API");
Console.ResetColor();
Console.WriteLine("  │  ├─ GET  /api/orders          → List all orders (desc)");
Console.WriteLine("  │  └─ GET  /api/orders/{id}     → Get order by ID");
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ┌─ Checkout API");
Console.ResetColor();
Console.WriteLine("  │  └─ POST /api/checkout        → Process checkout");
Console.WriteLine("\n" + new string('=', 80) + "\n");

app.Run();

// DTOs for API endpoints
record CheckoutRequest(string CustomerId, string PaymentToken);

// Make Program class accessible to test projects
namespace RetailDecomposed
{
    public partial class Program { }
}

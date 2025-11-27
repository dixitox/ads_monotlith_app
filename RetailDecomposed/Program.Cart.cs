using Microsoft.EntityFrameworkCore;
using RetailDecomposed.Data;
using RetailDecomposed.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON serialization
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

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
                       "Server=(localdb)\\MSSQLLocalDB;Database=RetailDecomposed;Trusted_Connection=True;MultipleActiveResultSets=true"));
}

// Register Cart service
builder.Services.AddScoped<ICartService, CartService>();

// Register HttpClient for Products Service
var productsServiceUrl = builder.Configuration["ProductsServiceUrl"] ?? "http://localhost:8081";
builder.Services.AddHttpClient("ProductsService", client =>
{
    client.BaseAddress = new Uri(productsServiceUrl);
});

builder.Services.AddHealthChecks();

var app = builder.Build();

// Run migrations (Cart tables)
if (app.Environment.EnvironmentName != "Testing")
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
}

app.UseRouting();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "cart" }));

// Cart API endpoints
app.MapGet("/api/cart/{customerId}", async (string customerId, ICartService cart) =>
{
    var cartData = await cart.GetCartWithLinesAsync(customerId);
    return Results.Ok(cartData);
});

app.MapPost("/api/cart/{customerId}/items", async (string customerId, int productId, int quantity, ICartService cart) =>
{
    await cart.AddToCartAsync(customerId, productId, quantity);
    return Results.Ok(new { message = "Item added to cart" });
});

app.MapDelete("/api/cart/{customerId}/items/{sku}", async (string customerId, string sku, ICartService cart) =>
{
    await cart.RemoveFromCartAsync(customerId, sku);
    return Results.Ok(new { message = "Item removed from cart" });
});

app.MapDelete("/api/cart/{customerId}", async (string customerId, ICartService cart) =>
{
    await cart.ClearCartAsync(customerId);
    return Results.Ok(new { message = "Cart cleared" });
});

// Display service info
var urls = app.Urls.FirstOrDefault() ?? "http://localhost:8082";
Console.WriteLine("\n" + new string('=', 60));
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("  CART SERVICE");
Console.ResetColor();
Console.WriteLine(new string('=', 60));
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"\n  Service running at: {urls}\n");
Console.ResetColor();
Console.WriteLine("  API Endpoints:");
Console.WriteLine("  ├─ GET    /health                         → Health check");
Console.WriteLine("  ├─ GET    /api/cart/{customerId}          → Get cart");
Console.WriteLine("  ├─ POST   /api/cart/{customerId}/items    → Add item");
Console.WriteLine("  ├─ DELETE /api/cart/{customerId}/items/{sku} → Remove item");
Console.WriteLine("  └─ DELETE /api/cart/{customerId}          → Clear cart");
Console.WriteLine("\n  Dependencies:");
Console.WriteLine($"  └─ Products Service: {productsServiceUrl}");
Console.WriteLine("\n" + new string('=', 60) + "\n");

app.Run();

// Make Program class accessible to test projects
namespace RetailDecomposed.CartApi
{
    public partial class Program { }
}

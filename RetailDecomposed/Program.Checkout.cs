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

// Register Checkout service
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IPaymentGateway, MockPaymentGateway>();

// Register HttpClients for other services
var cartServiceUrl = builder.Configuration["CartServiceUrl"] ?? "http://localhost:8082";
var productsServiceUrl = builder.Configuration["ProductsServiceUrl"] ?? "http://localhost:8081";
var ordersServiceUrl = builder.Configuration["OrdersServiceUrl"] ?? "http://localhost:8083";

builder.Services.AddHttpClient("CartService", client =>
{
    client.BaseAddress = new Uri(cartServiceUrl);
});

builder.Services.AddHttpClient("ProductsService", client =>
{
    client.BaseAddress = new Uri(productsServiceUrl);
});

builder.Services.AddHttpClient("OrdersService", client =>
{
    client.BaseAddress = new Uri(ordersServiceUrl);
});

builder.Services.AddHealthChecks();

var app = builder.Build();

// Run migrations
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
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "checkout" }));

// Checkout API endpoint
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
});

// Display service info
var urls = app.Urls.FirstOrDefault() ?? "http://localhost:8084";
Console.WriteLine("\n" + new string('=', 60));
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("  CHECKOUT SERVICE");
Console.ResetColor();
Console.WriteLine(new string('=', 60));
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"\n  Service running at: {urls}\n");
Console.ResetColor();
Console.WriteLine("  API Endpoints:");
Console.WriteLine("  ├─ GET  /health           → Health check");
Console.WriteLine("  └─ POST /api/checkout     → Process checkout");
Console.WriteLine("\n  Dependencies:");
Console.WriteLine($"  ├─ Cart Service:     {cartServiceUrl}");
Console.WriteLine($"  ├─ Products Service: {productsServiceUrl}");
Console.WriteLine($"  └─ Orders Service:   {ordersServiceUrl}");
Console.WriteLine("\n" + new string('=', 60) + "\n");

app.Run();

// DTO for checkout request
record CheckoutRequest(string CustomerId, string PaymentToken);

// Make Program class accessible to test projects
namespace RetailDecomposed.Checkout
{
    public partial class Program { }
}

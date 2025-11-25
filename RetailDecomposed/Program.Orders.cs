using Microsoft.EntityFrameworkCore;
using RetailDecomposed.Data;
using System.Security.Claims;
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

builder.Services.AddHealthChecks();

var app = builder.Build();

// Run migrations (Orders tables)
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
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "orders" }));

// Orders API endpoints
app.MapGet("/api/orders", async (AppDbContext db, string? customerId = null) =>
{
    IQueryable<RetailDecomposed.Models.Order> query = db.Orders.Include(o => o.Lines);
    
    // Filter by customerId if provided
    if (!string.IsNullOrEmpty(customerId))
    {
        query = query.Where(o => o.CustomerId == customerId);
    }
    
    var orders = await query.OrderByDescending(o => o.CreatedUtc).ToListAsync();
    return Results.Ok(orders);
});

app.MapGet("/api/orders/{id}", async (int id, AppDbContext db) =>
{
    var order = await db.Orders
        .Include(o => o.Lines)
        .FirstOrDefaultAsync(o => o.Id == id);
    if (order is null)
        return Results.NotFound();
    return Results.Ok(order);
});

app.MapGet("/api/orders/customer/{customerId}", async (string customerId, AppDbContext db) =>
{
    var orders = await db.Orders
        .Include(o => o.Lines)
        .Where(o => o.CustomerId == customerId)
        .OrderByDescending(o => o.CreatedUtc)
        .ToListAsync();
    return Results.Ok(orders);
});

// Display service info
var urls = app.Urls.FirstOrDefault() ?? "http://localhost:8083";
Console.WriteLine("\n" + new string('=', 60));
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("  ORDERS SERVICE");
Console.ResetColor();
Console.WriteLine(new string('=', 60));
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"\n  Service running at: {urls}\n");
Console.ResetColor();
Console.WriteLine("  API Endpoints:");
Console.WriteLine("  ├─ GET  /health                          → Health check");
Console.WriteLine("  ├─ GET  /api/orders                      → List all orders");
Console.WriteLine("  ├─ GET  /api/orders?customerId={id}      → Filter by customer");
Console.WriteLine("  ├─ GET  /api/orders/{id}                 → Get order by ID");
Console.WriteLine("  └─ GET  /api/orders/customer/{id}        → Get customer orders");
Console.WriteLine("\n" + new string('=', 60) + "\n");

app.Run();

// Make Program class accessible to test projects
namespace RetailDecomposed.Orders
{
    public partial class Program { }
}

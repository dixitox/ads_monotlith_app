using Microsoft.EntityFrameworkCore;
using RetailDecomposed.Data;
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

// Run migrations and seed data
if (app.Environment.EnvironmentName != "Testing")
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await AppDbContext.SeedAsync(db);
    }
}

app.UseRouting();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "products" }));

// Products API endpoints
app.MapGet("/api/products", async (AppDbContext db) =>
{
    var products = await db.Products
        .Where(p => p.IsActive)
        .ToListAsync();
    return Results.Ok(products);
});

app.MapGet("/api/products/{id}", async (int id, AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    if (product is null || !product.IsActive)
        return Results.NotFound();
    return Results.Ok(product);
});

// Optional: Filter by category
app.MapGet("/api/products/category/{category}", async (string category, AppDbContext db) =>
{
    var products = await db.Products
        .Where(p => p.IsActive && p.Category == category)
        .ToListAsync();
    return Results.Ok(products);
});

// Display service info
var urls = app.Urls.FirstOrDefault() ?? "http://localhost:8081";
Console.WriteLine("\n" + new string('=', 60));
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("  PRODUCTS SERVICE");
Console.ResetColor();
Console.WriteLine(new string('=', 60));
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"\n  Service running at: {urls}\n");
Console.ResetColor();
Console.WriteLine("  API Endpoints:");
Console.WriteLine("  ├─ GET  /health                        → Health check");
Console.WriteLine("  ├─ GET  /api/products                  → List all products");
Console.WriteLine("  ├─ GET  /api/products/{id}             → Get product by ID");
Console.WriteLine("  └─ GET  /api/products/category/{name}  → Filter by category");
Console.WriteLine("\n" + new string('=', 60) + "\n");

app.Run();

// Make Program class accessible to test projects
namespace RetailDecomposed.Products
{
    public partial class Program { }
}

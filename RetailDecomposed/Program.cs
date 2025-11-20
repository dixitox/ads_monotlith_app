using Microsoft.EntityFrameworkCore;
using RetailMonolith.Data;
using RetailMonolith.Services;
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

builder.Services.AddRazorPages();
builder.Services.AddScoped<IPaymentGateway, MockPaymentGateway>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<ICartService, CartService>();

// Register Cart API Client for decomposed Cart module
builder.Services.AddHttpClient<RetailDecomposed.Services.ICartApiClient, RetailDecomposed.Services.CartApiClient>(client =>
{
    client.BaseAddress = new Uri("https://localhost:8108");
});

// Register Products API Client for decomposed Products module
builder.Services.AddHttpClient<RetailDecomposed.Services.IProductsApiClient, RetailDecomposed.Services.ProductsApiClient>(client =>
{
    client.BaseAddress = new Uri("https://localhost:8108");
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
app.UseAuthorization();
app.MapRazorPages();

// Cart API surface for decomposition
app.MapGet("/api/cart/{customerId}", async (string customerId, ICartService cart) =>
{
    var cartData = await cart.GetCartWithLinesAsync(customerId);
    return Results.Ok(cartData);
});

app.MapPost("/api/cart/{customerId}/items", async (string customerId, int productId, int quantity, ICartService cart) =>
{
    await cart.AddToCartAsync(customerId, productId, quantity);
    return Results.Ok();
});

// Products API surface for decomposition
app.MapGet("/api/products", async (AppDbContext db) =>
{
    var products = await db.Products.Where(p => p.IsActive).ToListAsync();
    return Results.Ok(products);
});

app.MapGet("/api/products/{id}", async (int id, AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    if (product is null)
        return Results.NotFound();
    return Results.Ok(product);
});

app.Run();

// Make Program class accessible to test projects
namespace RetailDecomposed
{
    public partial class Program { }
}

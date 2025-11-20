using Microsoft.EntityFrameworkCore;
using RetailMonolith.Data;
using RetailMonolith.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ??
                   "Server=(localdb)\\MSSQLLocalDB;Database=RetailMonolith;Trusted_Connection=True;MultipleActiveResultSets=true"));

builder.Services.AddRazorPages();
builder.Services.AddScoped<IPaymentGateway, MockPaymentGateway>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<ICartService, CartService>();

// Register Cart API Client for decomposed Cart module
builder.Services.AddHttpClient<RetailDecomposed.Services.ICartApiClient, RetailDecomposed.Services.CartApiClient>(client =>
{
    client.BaseAddress = new Uri("https://localhost:8108");
});

builder.Services.AddHealthChecks();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await AppDbContext.SeedAsync(db);
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

app.Run();

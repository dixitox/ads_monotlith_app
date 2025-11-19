using Microsoft.EntityFrameworkCore;
using RetailMonolith.Checkout.Api.Data;
using RetailMonolith.Checkout.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddControllers();

// Configure DbContext - use in-memory for tests, SQL Server for production
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString) || connectionString.Contains("InMemory"))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("CheckoutApiDb"));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString));
}

// Register payment gateway
builder.Services.AddScoped<IPaymentGateway, MockPaymentGateway>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Map controllers and health checks
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Make the implicit Program class public for testing
public partial class Program { }

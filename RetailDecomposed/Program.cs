using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using RetailDecomposed.Data;
using RetailDecomposed.Services;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry with Application Insights
var connectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(
                serviceName: "RetailDecomposed-Frontend",
                serviceVersion: "1.0.0",
                serviceInstanceId: Environment.MachineName))
        .UseAzureMonitor(options =>
        {
            options.ConnectionString = connectionString;
        })
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.EnrichWithHttpRequest = (activity, httpRequest) =>
                {
                    activity.SetTag("http.request.user", httpRequest.HttpContext.User?.Identity?.Name ?? "anonymous");
                };
                options.EnrichWithHttpResponse = (activity, httpResponse) =>
                {
                    activity.SetTag("http.response.content_length", httpResponse.ContentLength);
                };
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
                options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                {
                    activity.SetTag("http.request.url", httpRequestMessage.RequestUri?.ToString());
                };
                options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
                {
                    activity.SetTag("http.response.status_code", (int)httpResponseMessage.StatusCode);
                };
            })
            .AddSqlClientInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddSource("RetailDecomposed.Services.*"))
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation());
}
else
{
    Console.WriteLine("Application Insights not configured - telemetry disabled");
}

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

// Configure JSON serialization
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Check if Azure AD is configured
var azureAdConfig = builder.Configuration.GetSection("AzureAd");
var tenantId = azureAdConfig["TenantId"];
var clientId = azureAdConfig["ClientId"];

var isAzureAdConfigured = !string.IsNullOrEmpty(clientId) &&
                          Guid.TryParse(clientId, out _) &&
                          !string.IsNullOrEmpty(tenantId) &&
                          Guid.TryParse(tenantId, out _);

var requireAuthorizationConfig = builder.Configuration.GetValue<bool?>("RequireAuthorization");
var isTesting = builder.Environment.IsEnvironment("Testing");
var requireAuthorization = requireAuthorizationConfig ?? (isAzureAdConfigured || isTesting);

if (isAzureAdConfigured)
{
    Console.WriteLine($"Using Azure AD authentication with TenantId: {tenantId}");
}
else
{
    Console.WriteLine("Azure AD not configured - using no-auth mode");
}

// Add Microsoft Entra ID authentication
if (isAzureAdConfigured)
{
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(options =>
        {
            builder.Configuration.Bind("AzureAd", options);
            options.SignedOutRedirectUri = "/";
            options.TokenValidationParameters.RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
        });
}
else if (!builder.Environment.IsEnvironment("Testing"))
{
    // Only add NoAuth handler in non-testing environments
    // In testing, the test framework provides its own authentication handler
    builder.Services.AddAuthentication("NoAuth")
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, NoAuthHandler>("NoAuth", options => { });
}

// Add authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));
    options.AddPolicy("CustomerAccess", policy =>
        policy.RequireAuthenticatedUser());
});

// Add Razor Pages and Controllers
if (isAzureAdConfigured)
{
    builder.Services.AddRazorPages(options =>
    {
        if (requireAuthorization)
        {
            options.Conventions.AuthorizePage("/Copilot/Index", "CustomerAccess");
        }
    })
        .AddMicrosoftIdentityUI();
    builder.Services.AddControllers();
}
else
{
    builder.Services.AddRazorPages(options =>
    {
        if (requireAuthorization)
        {
            options.Conventions.AuthorizePage("/Copilot/Index", "CustomerAccess");
        }
    });
}

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

// Register HttpContext accessor for cookie propagation
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<RetailDecomposed.Services.CookiePropagatingHandler>();

// Helper to create HttpClient handlers
static HttpMessageHandler CreateHttpMessageHandler(bool isDevelopment)
{
    var handler = new HttpClientHandler();
    if (isDevelopment)
    {
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    else
    {
        if (handler.ServerCertificateCustomValidationCallback == HttpClientHandler.DangerousAcceptAnyServerCertificateValidator)
        {
            throw new InvalidOperationException(
                "DangerousAcceptAnyServerCertificateValidator must NEVER be used in production.");
        }
    }
    return handler;
}

// Get service URLs from configuration
var productsServiceUrl = builder.Configuration["ProductsServiceUrl"] ?? "http://localhost:8081";
var cartServiceUrl = builder.Configuration["CartServiceUrl"] ?? "http://localhost:8082";
var ordersServiceUrl = builder.Configuration["OrdersServiceUrl"] ?? "http://localhost:8083";
var checkoutServiceUrl = builder.Configuration["CheckoutServiceUrl"] ?? "http://localhost:8084";

// Register HttpClients for all backend services (API Clients)
builder.Services.AddHttpClient<IProductsApiClient, ProductsApiClient>(client =>
{
    client.BaseAddress = new Uri(productsServiceUrl);
})
.AddHttpMessageHandler<CookiePropagatingHandler>()
.ConfigurePrimaryHttpMessageHandler(() => CreateHttpMessageHandler(builder.Environment.IsDevelopment()));

builder.Services.AddHttpClient<ICartApiClient, CartApiClient>(client =>
{
    client.BaseAddress = new Uri(cartServiceUrl);
})
.AddHttpMessageHandler<CookiePropagatingHandler>()
.ConfigurePrimaryHttpMessageHandler(() => CreateHttpMessageHandler(builder.Environment.IsDevelopment()));

builder.Services.AddHttpClient<IOrdersApiClient, OrdersApiClient>(client =>
{
    client.BaseAddress = new Uri(ordersServiceUrl);
})
.AddHttpMessageHandler<CookiePropagatingHandler>()
.ConfigurePrimaryHttpMessageHandler(() => CreateHttpMessageHandler(builder.Environment.IsDevelopment()));

builder.Services.AddHttpClient<ICheckoutApiClient, CheckoutApiClient>(client =>
{
    client.BaseAddress = new Uri(checkoutServiceUrl);
})
.AddHttpMessageHandler<CookiePropagatingHandler>()
.ConfigurePrimaryHttpMessageHandler(() => CreateHttpMessageHandler(builder.Environment.IsDevelopment()));

// Register AI services (Copilot and Semantic Search)
builder.Services.AddScoped<ICopilotService, CopilotService>();
builder.Services.AddScoped<ISemanticSearchService, SemanticSearchService>();

// Register services for local API endpoints (used in testing and local development)
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IPaymentGateway, MockPaymentGateway>();

builder.Services.AddHealthChecks();

var app = builder.Build();

// Run migrations and seed data (Frontend manages schema)
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
app.MapControllers();

// ============================================================================
// API ENDPOINTS (for testing and local development)
// ============================================================================

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "frontend" }));

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

app.MapGet("/api/products/category/{category}", async (string category, AppDbContext db) =>
{
    var products = await db.Products
        .Where(p => p.IsActive && p.Category == category)
        .ToListAsync();
    return Results.Ok(products);
});

// Cart API endpoints (require authentication and user match)
app.MapGet("/api/cart/{customerId}", async (string customerId, ICartService cart, HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();
    
    var userId = context.User.Identity?.Name;
    if (userId != customerId)
        return Results.Forbid();
    
    var cartData = await cart.GetCartWithLinesAsync(customerId);
    return Results.Ok(cartData);
});

app.MapPost("/api/cart/{customerId}/items", async (string customerId, int productId, int quantity, ICartService cart, HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();
    
    var userId = context.User.Identity?.Name;
    if (userId != customerId)
        return Results.Forbid();
    
    await cart.AddToCartAsync(customerId, productId, quantity);
    return Results.Ok(new { message = "Item added to cart" });
});

app.MapDelete("/api/cart/{customerId}/items/{sku}", async (string customerId, string sku, ICartService cart, HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();
    
    var userId = context.User.Identity?.Name;
    if (userId != customerId)
        return Results.Forbid();
    
    await cart.RemoveFromCartAsync(customerId, sku);
    return Results.Ok(new { message = "Item removed from cart" });
});

app.MapDelete("/api/cart/{customerId}", async (string customerId, ICartService cart, HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();
    
    var userId = context.User.Identity?.Name;
    if (userId != customerId)
        return Results.Forbid();
    
    await cart.ClearCartAsync(customerId);
    return Results.Ok(new { message = "Cart cleared" });
});

// Orders API endpoints
app.MapGet("/api/orders", async (AppDbContext db, string? customerId = null) =>
{
    IQueryable<RetailDecomposed.Models.Order> query = db.Orders.Include(o => o.Lines);
    
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

// Chat/Copilot API endpoint
app.MapPost("/api/chat", async (ChatApiRequest request, ICopilotService copilotService, HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();
    
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { error = "Message cannot be empty" });
    
    try
    {
        var response = await copilotService.GetChatResponseAsync(request.Message, request.ConversationHistory);
        return Results.Ok(new { response });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// ============================================================================

// Display service info
var urls = app.Urls.FirstOrDefault() ?? "http://localhost:8080";
Console.WriteLine("\n" + new string('=', 80));
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("  FRONTEND SERVICE (Razor Pages UI)");
Console.ResetColor();
Console.WriteLine(new string('=', 80));
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"\n  Service running at: {urls}\n");
Console.ResetColor();
Console.WriteLine("  Pages:");
Console.WriteLine("  ├─ GET  /                      → Home page");
Console.WriteLine("  ├─ GET  /Products              → Product catalog");
Console.WriteLine("  ├─ GET  /Cart                  → Shopping cart");
Console.WriteLine("  ├─ GET  /Checkout              → Checkout page");
Console.WriteLine("  ├─ GET  /Orders                → Order history");
Console.WriteLine("  ├─ GET  /Orders/{id}           → Order details");
Console.WriteLine("  ├─ GET  /Search                → Semantic search");
Console.WriteLine("  └─ GET  /Copilot               → AI Copilot chat");
Console.WriteLine("\n  Backend Services:");
Console.WriteLine($"  ├─ Products:  {productsServiceUrl}");
Console.WriteLine($"  ├─ Cart:      {cartServiceUrl}");
Console.WriteLine($"  ├─ Orders:    {ordersServiceUrl}");
Console.WriteLine($"  └─ Checkout:  {checkoutServiceUrl}");
Console.WriteLine("\n  Authentication:");
Console.WriteLine($"  └─ Azure AD:  {(isAzureAdConfigured ? "Configured" : "Not configured (NoAuth mode)")}");
Console.WriteLine("\n" + new string('=', 80) + "\n");

app.Run();

// Dummy authentication handler for no-auth mode
public class NoAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public NoAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "guest"),
            new Claim(ClaimTypes.NameIdentifier, "guest"),
            new Claim(ClaimTypes.Email, "guest@example.com")
        };
        
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        await Response.WriteAsync("Authentication required.");
    }
}

// DTOs for API endpoints
record CheckoutRequest(string CustomerId, string PaymentToken);

record ChatApiRequest(string Message, List<RetailDecomposed.Services.ChatMessage>? ConversationHistory);

// Make Program class accessible to test projects
namespace RetailDecomposed
{
    public partial class Program { }
}

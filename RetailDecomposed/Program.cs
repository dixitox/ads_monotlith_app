using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using RetailMonolith.Data;
using RetailMonolith.Services;
using System.Security.Claims;
using System.Text.Encodings.Web;
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

// Check if Azure AD is configured (not using placeholder values)
var azureAdConfig = builder.Configuration.GetSection("AzureAd");
var tenantId = azureAdConfig["TenantId"];
var clientId = azureAdConfig["ClientId"];

var isAzureAdConfigured = !string.IsNullOrEmpty(clientId) &&
                          Guid.TryParse(clientId, out _) &&
                          !string.IsNullOrEmpty(tenantId) &&
                          Guid.TryParse(tenantId, out _);

// Determine if authorization should be required
// Priority: Explicit configuration > Azure AD configured > Testing environment
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
            
            // Map Azure AD roles to ASP.NET Core roles
            // Azure AD sends roles using this specific claim type
            options.TokenValidationParameters.RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
        });
}
else
{
    // Fallback to NoAuth handler for development without Azure AD
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

// Add MicrosoftIdentityUI for sign-in/sign-out when Azure AD is configured
if (isAzureAdConfigured)
{
    builder.Services.AddRazorPages(options =>
    {
        // Require authentication for specific pages when authorization is required
        if (requireAuthorization)
        {
            options.Conventions.AuthorizePage("/Copilot/Index", "CustomerAccess");
        }
    })
        .AddMicrosoftIdentityUI();
    // Add controllers for MicrosoftIdentity UI
    builder.Services.AddControllers();
}
else
{
    builder.Services.AddRazorPages(options =>
    {
        // Require authentication for specific pages when authorization is required
        if (requireAuthorization)
        {
            options.Conventions.AuthorizePage("/Copilot/Index", "CustomerAccess");
        }
    });
}

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

// Register cookie propagating handler for API calls
builder.Services.AddTransient<RetailDecomposed.Services.CookiePropagatingHandler>();

// WARNING: Never use DangerousAcceptAnyServerCertificateValidator in production!
// This bypasses ALL SSL certificate validation and exposes the application to MITM attacks.
// Ensure this is ONLY enabled in development. If enabled in any other environment, fail fast.
static HttpMessageHandler CreateHttpMessageHandler(bool isDevelopment)
{
    var handler = new HttpClientHandler();
    if (isDevelopment)
    {
        // Allow untrusted certificates in development only
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    else
    {
        // Defensive: If this ever gets enabled in non-development, throw.
        if (handler.ServerCertificateCustomValidationCallback == HttpClientHandler.DangerousAcceptAnyServerCertificateValidator)
        {
            throw new InvalidOperationException(
                "DangerousAcceptAnyServerCertificateValidator must NEVER be used in production. Check environment configuration.");
        }
    }
    return handler;
}

// Register Cart API Client for decomposed Cart module
builder.Services.AddHttpClient<RetailDecomposed.Services.ICartApiClient, RetailDecomposed.Services.CartApiClient>(client =>
{
    client.BaseAddress = new Uri("https://localhost:6068");
})
.AddHttpMessageHandler<RetailDecomposed.Services.CookiePropagatingHandler>()
.ConfigurePrimaryHttpMessageHandler(() => CreateHttpMessageHandler(builder.Environment.IsDevelopment()));

// Register Products API Client for decomposed Products module
builder.Services.AddHttpClient<RetailDecomposed.Services.IProductsApiClient, RetailDecomposed.Services.ProductsApiClient>(client =>
{
    client.BaseAddress = new Uri("https://localhost:6068");
})
.AddHttpMessageHandler<RetailDecomposed.Services.CookiePropagatingHandler>()
.ConfigurePrimaryHttpMessageHandler(() => CreateHttpMessageHandler(builder.Environment.IsDevelopment()));

// Register AI Copilot service (depends on IProductsApiClient)
builder.Services.AddScoped<RetailDecomposed.Services.ICopilotService, RetailDecomposed.Services.CopilotService>();

// Register Orders API Client for decomposed Orders module
builder.Services.AddHttpClient<RetailDecomposed.Services.IOrdersApiClient, RetailDecomposed.Services.OrdersApiClient>(client =>
{
    client.BaseAddress = new Uri("https://localhost:6068");
})
.AddHttpMessageHandler<RetailDecomposed.Services.CookiePropagatingHandler>()
.ConfigurePrimaryHttpMessageHandler(() => CreateHttpMessageHandler(builder.Environment.IsDevelopment()));

// Register Checkout API Client for decomposed Checkout module
builder.Services.AddHttpClient<RetailDecomposed.Services.ICheckoutApiClient, RetailDecomposed.Services.CheckoutApiClient>(client =>
{
    client.BaseAddress = new Uri("https://localhost:6068");
})
.AddHttpMessageHandler<RetailDecomposed.Services.CookiePropagatingHandler>()
.ConfigurePrimaryHttpMessageHandler(() => CreateHttpMessageHandler(builder.Environment.IsDevelopment()));

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

// Use authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Only map controllers if Azure AD is configured (for MicrosoftIdentity area controllers)
if (isAzureAdConfigured)
{
    app.MapControllers();
}

// Helper method to validate that the authenticated user matches the requested customerId
static bool IsAuthorizedForCustomer(HttpContext httpContext, string customerId)
{
    var authenticatedUserId = httpContext.User.Identity?.Name;
    return !string.IsNullOrEmpty(authenticatedUserId) && authenticatedUserId == customerId;
}

// Helper method to apply authorization when Azure AD is configured
static RouteHandlerBuilder ApplyAuthorizationIfConfigured(RouteHandlerBuilder builder, bool isConfigured, string policyName)
{
    return isConfigured ? builder.RequireAuthorization(policyName) : builder.AllowAnonymous();
}

// Cart API surface for decomposition
ApplyAuthorizationIfConfigured(
    app.MapGet("/api/cart/{customerId}", async (string customerId, ICartService cart, ClaimsPrincipal user) =>
    {
        // Validate that the authenticated user matches the customerId (only if authorization is required)
        if (requireAuthorization)
        {
            var authenticatedUserId = user.Identity?.Name;
            if (authenticatedUserId != customerId)
            {
                return Results.Forbid();
            }
        }
        
        var cartData = await cart.GetCartWithLinesAsync(customerId);
        return Results.Ok(cartData);
    }),
    requireAuthorization,
    "CustomerAccess"
);

ApplyAuthorizationIfConfigured(
    app.MapPost("/api/cart/{customerId}/items", async (string customerId, int productId, int quantity, ICartService cart, ClaimsPrincipal user) =>
    {
        // Validate that the authenticated user matches the customerId (only if authorization is required)
        if (requireAuthorization)
        {
            var authenticatedUserId = user.Identity?.Name;
            if (authenticatedUserId != customerId)
            {
                return Results.Forbid();
            }
        }
        
        await cart.AddToCartAsync(customerId, productId, quantity);
        return Results.Ok();
    }),
    requireAuthorization,
    "CustomerAccess"
);

ApplyAuthorizationIfConfigured(
    app.MapDelete("/api/cart/{customerId}/items/{sku}", async (string customerId, string sku, ICartService cart, ClaimsPrincipal user) =>
    {
        // Validate that the authenticated user matches the customerId (only if authorization is required)
        if (requireAuthorization)
        {
            var authenticatedUserId = user.Identity?.Name;
            if (authenticatedUserId != customerId)
            {
                return Results.Forbid();
            }
        }
        
        await cart.RemoveFromCartAsync(customerId, sku);
        return Results.Ok();
    }),
    requireAuthorization,
    "CustomerAccess"
);

ApplyAuthorizationIfConfigured(
    app.MapDelete("/api/cart/{customerId}", async (string customerId, ICartService cart, ClaimsPrincipal user) =>
    {
        // Validate that the authenticated user matches the customerId (only if authorization is required)
        if (requireAuthorization)
        {
            var authenticatedUserId = user.Identity?.Name;
            if (authenticatedUserId != customerId)
            {
                return Results.Forbid();
            }
        }
        
        await cart.ClearCartAsync(customerId);
        return Results.Ok();
    }),
    requireAuthorization,
    "CustomerAccess"
);

// Products API surface for decomposition
ApplyAuthorizationIfConfigured(
    app.MapGet("/api/products", async (AppDbContext db) =>
    {
        var products = await db.Products.Where(p => p.IsActive).ToListAsync();
        return Results.Ok(products);
    }),
    requireAuthorization,
    "CustomerAccess"
);

ApplyAuthorizationIfConfigured(
    app.MapGet("/api/products/{id}", async (int id, AppDbContext db) =>
    {
        var product = await db.Products.FindAsync(id);
        if (product is null)
            return Results.NotFound();
        return Results.Ok(product);
    }),
    requireAuthorization,
    "CustomerAccess"
);

// Orders API surface for decomposition
ApplyAuthorizationIfConfigured(
    app.MapGet("/api/orders", async (AppDbContext db, ClaimsPrincipal user, ILogger<Program> logger) =>
    {
        // If user is Admin, return all orders. Otherwise, return only their orders.
        var userId = user.Identity?.Name;
        var isAdmin = user.IsInRole("Admin");
        
        logger.LogInformation("Orders API called by user: {UserId}, IsAdmin: {IsAdmin}, IsAuthenticated: {IsAuthenticated}", 
            userId, isAdmin, user.Identity?.IsAuthenticated);
        
        IQueryable<RetailMonolith.Models.Order> query = db.Orders.Include(o => o.Lines);
        
        // Only filter by user if authorization is required and user is not admin
        if (requireAuthorization && !isAdmin && !string.IsNullOrEmpty(userId))
        {
            logger.LogInformation("Filtering orders for user: {UserId}", userId);
            query = query.Where(o => o.CustomerId == userId);
        }
        else if (isAdmin)
        {
            logger.LogInformation("Admin user - returning all orders");
        }
        
        var orders = await query.OrderByDescending(o => o.CreatedUtc).ToListAsync();
        logger.LogInformation("Returning {OrderCount} orders", orders.Count);
        return Results.Ok(orders);
    }),
    requireAuthorization,
    "CustomerAccess"
);

ApplyAuthorizationIfConfigured(
    app.MapGet("/api/orders/{id}", async (int id, AppDbContext db) =>
    {
        var order = await db.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order is null)
            return Results.NotFound();
        return Results.Ok(order);
    }),
    requireAuthorization,
    "CustomerAccess"
);

// Checkout API surface for decomposition
ApplyAuthorizationIfConfigured(
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
    }),
    requireAuthorization,
    "CustomerAccess"
);

// AI Copilot Chat API
ApplyAuthorizationIfConfigured(
    app.MapPost("/api/chat", async (ChatRequest request, RetailDecomposed.Services.ICopilotService copilotService) =>
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.BadRequest(new { error = "Message cannot be empty" });
        }

        try
        {
            var response = await copilotService.GetChatResponseAsync(
                request.Message,
                request.ConversationHistory);
            
            return Results.Ok(new { response });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: "An error occurred processing your request",
                statusCode: 500);
        }
    }),
    requireAuthorization,
    "CustomerAccess"
);

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
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  ┌─ AI Copilot API");
Console.ResetColor();
Console.WriteLine("  │  └─ POST /api/chat            → Chat with AI assistant");
Console.WriteLine("\n" + new string('=', 80) + "\n");

app.Run();

// Dummy authentication handler that allows all requests without authentication
// Provides a fake "guest" user identity for development without Azure AD
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
        // Create a fake guest user identity with required claims
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
        // Return 401 Unauthorized to indicate authentication failure
        Response.StatusCode = 401;
        await Response.WriteAsync("Authentication required.");
    }
}

// DTOs for API endpoints
record CheckoutRequest(string CustomerId, string PaymentToken);
record ChatRequest(string Message, List<RetailDecomposed.Services.ChatMessage>? ConversationHistory);

// Make Program class accessible to test projects
namespace RetailDecomposed
{
    public partial class Program { }
}

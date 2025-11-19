using Microsoft.EntityFrameworkCore;
using RetailMonolith.Data;
using RetailMonolith.Models;
using RetailMonolith.Services;
using Microsoft.Extensions.Options;


var builder = WebApplication.CreateBuilder(args);

// Configure Azure credentials from environment variables (GitHub secrets)
// Environment variables override appsettings.json values
var azureSearchEndpoint = Environment.GetEnvironmentVariable("AZURE_SEARCH_ENDPOINT");
var azureSearchApiKey = Environment.GetEnvironmentVariable("AZURE_SEARCH_API_KEY");
var azureOpenAIEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
var azureOpenAIApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

if (!string.IsNullOrEmpty(azureSearchEndpoint))
    builder.Configuration["AzureSearch:Endpoint"] = azureSearchEndpoint;
if (!string.IsNullOrEmpty(azureSearchApiKey))
    builder.Configuration["AzureSearch:ApiKey"] = azureSearchApiKey;
if (!string.IsNullOrEmpty(azureOpenAIEndpoint))
    builder.Configuration["AzureOpenAI:Endpoint"] = azureOpenAIEndpoint;
if (!string.IsNullOrEmpty(azureOpenAIApiKey))
    builder.Configuration["AzureOpenAI:ApiKey"] = azureOpenAIApiKey;

// DB � localdb for hack; swap to SQL in appsettings for Azure
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ??
                   "Server=(localdb)\\MSSQLLocalDB;Database=RetailMonolith;Trusted_Connection=True;MultipleActiveResultSets=true"));


// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddScoped<IPaymentGateway, MockPaymentGateway>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddHealthChecks();

// Configure Azure Search settings
builder.Services.Configure<AzureOpenAIConfiguration>(
    builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.Configure<AzureSearchConfiguration>(
    builder.Configuration.GetSection("AzureSearch"));

// Register SearchService with IOptions injection
builder.Services.AddScoped<ISearchService>(sp =>
{
    var db = sp.GetRequiredService<AppDbContext>();
    var logger = sp.GetRequiredService<ILogger<SearchService>>();
    var openAIOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AzureOpenAIConfiguration>>();
    var searchOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AzureSearchConfiguration>>();
    return new SearchService(db, logger, openAIOptions.Value, searchOptions.Value);
});

var app = builder.Build();

// auto-migrate & seed (hack convenience)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await AppDbContext.SeedAsync(db); // seed the database
}


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();


// minimal APIs for the �decomp� path
app.MapPost("/api/checkout", async (ICheckoutService svc) =>
{
    var order = await svc.CheckoutAsync("guest", "tok_test");
    return Results.Ok(new { order.Id, order.Status, order.Total });
});

app.MapGet("/api/orders/{id:int}", async (int id, AppDbContext db) =>
{
    var order = await db.Orders.Include(o => o.Lines)
        .SingleOrDefaultAsync(o => o.Id == id);

    return order is null ? Results.NotFound() : Results.Ok(order);
});

// Chat API endpoint
app.MapPost("/api/chat", async (IChatService chatService, ChatRequest request) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.BadRequest(new ChatResponse 
            { 
                Success = false, 
                Error = "Message cannot be empty",
                Message = string.Empty
            });
        }

        var historyJson = System.Text.Json.JsonSerializer.Serialize(request.History);
        var response = await chatService.GetChatResponseAsync(request.Message, historyJson);

        return Results.Ok(new ChatResponse 
        { 
            Success = true, 
            Message = response 
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new ChatResponse 
        { 
            Success = false, 
            Error = ex.Message,
            Message = "Sorry, I encountered an error. Please try again."
        }, statusCode: 500);
    }
});

app.MapPost("/admin/search/initialize", async (ISearchService searchService) =>
{
    await searchService.InitializeIndexAsync();
    await searchService.IndexProductsAsync();
    return Results.Ok(new { message = "Search index initialized successfully" });
}).WithName("InitializeSearch");

app.Run();

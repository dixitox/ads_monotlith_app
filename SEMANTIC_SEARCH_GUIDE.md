# Semantic Search Implementation Guide

This guide walks you through adding natural-language product discovery to your Retail Monolith app using Azure AI Search and embeddings.

## Overview

**What you'll build:**
- Natural language product search (e.g., "comfortable shoes for running")
- Vector embeddings for semantic similarity
- Hybrid search combining keyword + semantic matching
- Azure AI Search integration

**Architecture Changes:**
- Add Azure OpenAI for generating embeddings
- Add Azure AI Search for vector search
- Create `SearchService` for product discovery
- Add search UI to Products page

---

## Step 1: Azure Resources Setup

### 1.1 Create Azure OpenAI Resource

1. **Sign in to Azure Portal**: https://portal.azure.com
2. **Create Resource** → Search "Azure OpenAI" → Click "Create"
3. **Configure:**
   - **Subscription**: Select your subscription
   - **Resource Group**: Create new or use existing (e.g., `rg-retail-app`)
   - **Region**: Select supported region (e.g., `East US`, `West Europe`)
   - **Name**: e.g., `openai-retail-search`
   - **Pricing Tier**: `Standard S0`
4. **Review + Create** → Wait for deployment
5. **Deploy Model:**
   - Go to your Azure OpenAI resource
   - Click "Model deployments" → "Manage Deployments"
   - This opens Azure OpenAI Studio
   - Click "Create new deployment"
   - **Model**: `text-embedding-ada-002` (or `text-embedding-3-small`)
   - **Deployment Name**: `text-embedding-ada-002`
   - **Deploy**
6. **Get Credentials:**
   - Go back to your resource in Azure Portal
   - Navigate to "Keys and Endpoint"
   - Copy:
     - **Endpoint**: e.g., `https://openai-retail-search.openai.azure.com/`
     - **Key 1**: (your API key)

### 1.2 Create Azure AI Search Resource

1. **Create Resource** → Search "Azure AI Search" → Click "Create"
2. **Configure:**
   - **Subscription**: Same as above
   - **Resource Group**: Same as above (e.g., `rg-retail-app`)
   - **Service Name**: e.g., `search-retail-products` (must be globally unique)
   - **Region**: Same region as OpenAI (for lower latency)
   - **Pricing Tier**: `Basic` (supports vector search, ~$75/month) or `Free` (for testing, limited features)
3. **Review + Create** → Wait for deployment
4. **Get Credentials:**
   - Go to your AI Search resource
   - Navigate to "Keys"
   - Copy:
     - **URL**: e.g., `https://search-retail-products.search.windows.net`
     - **Primary admin key**: (your API key)

### 1.3 Update Application Configuration

Add these settings to `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "..."
  },
  "AzureOpenAI": {
    "Endpoint": "https://openai-retail-search.openai.azure.com/",
    "ApiKey": "your-openai-api-key",
    "EmbeddingDeployment": "text-embedding-ada-002"
  },
  "AzureSearch": {
    "Endpoint": "https://search-retail-products.search.windows.net",
    "ApiKey": "your-search-api-key",
    "IndexName": "products"
  }
}
```

**For production**, use **Azure Key Vault** or **Managed Identity** instead of storing keys in config files.

Add to `appsettings.Development.json` for local testing:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AzureOpenAI": {
    "Endpoint": "https://openai-retail-search.openai.azure.com/",
    "ApiKey": "your-dev-openai-key",
    "EmbeddingDeployment": "text-embedding-ada-002"
  },
  "AzureSearch": {
    "Endpoint": "https://search-retail-products.search.windows.net",
    "ApiKey": "your-dev-search-key",
    "IndexName": "products"
  }
}
```

---

## Step 2: Install NuGet Packages

Add these packages to `RetailMonolith.csproj`:

```bash
dotnet add package Azure.Search.Documents --version 11.6.0
dotnet add package Azure.AI.OpenAI --version 2.1.0
```

Or manually add to `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Azure.Search.Documents" Version="11.6.0" />
  <PackageReference Include="Azure.AI.OpenAI" Version="2.1.0" />
</ItemGroup>
```

Then run:
```bash
dotnet restore
```

---

## Step 3: Code Implementation

### 3.1 Create Configuration Models

**File**: `Models/SearchConfiguration.cs`

```csharp
namespace RetailMonolith.Models
{
    public class AzureOpenAIConfiguration
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string EmbeddingDeployment { get; set; } = string.Empty;
    }

    public class AzureSearchConfiguration
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string IndexName { get; set; } = string.Empty;
    }
}
```

### 3.2 Create Search Index Model

**File**: `Models/ProductSearchDocument.cs`

```csharp
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace RetailMonolith.Models
{
    public class ProductSearchDocument
    {
        [SimpleField(IsKey = true, IsFilterable = true)]
        public string Id { get; set; } = string.Empty;

        [SearchableField(IsFilterable = true, IsSortable = true)]
        public string Sku { get; set; } = string.Empty;

        [SearchableField(IsSortable = true)]
        public string Name { get; set; } = string.Empty;

        [SearchableField]
        public string? Description { get; set; }

        [SearchableField(IsFilterable = true, IsFacetable = true)]
        public string? Category { get; set; }

        [SimpleField(IsFilterable = true, IsSortable = true)]
        public double Price { get; set; }

        [SimpleField(IsFilterable = true)]
        public bool IsActive { get; set; }

        // Vector field for semantic search
        [VectorSearchField(VectorSearchDimensions = 1536, VectorSearchProfileName = "vector-profile")]
        public IReadOnlyList<float>? Embedding { get; set; }
    }
}
```

### 3.3 Create Search Service Interface

**File**: `Services/ISearchService.cs`

```csharp
using RetailMonolith.Models;

namespace RetailMonolith.Services
{
    public interface ISearchService
    {
        Task InitializeIndexAsync(CancellationToken ct = default);
        Task IndexProductsAsync(CancellationToken ct = default);
        Task<IEnumerable<Product>> SearchProductsAsync(string query, int maxResults = 10, CancellationToken ct = default);
    }
}
```

### 3.4 Implement Search Service

**File**: `Services/SearchService.cs`

```csharp
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.EntityFrameworkCore;
using OpenAI.Embeddings;
using RetailMonolith.Data;
using RetailMonolith.Models;

namespace RetailMonolith.Services
{
    public class SearchService : ISearchService
    {
        private readonly AppDbContext _db;
        private readonly SearchIndexClient _indexClient;
        private readonly SearchClient _searchClient;
        private readonly AzureOpenAIClient _openAIClient;
        private readonly string _embeddingDeployment;

        public SearchService(
            AppDbContext db,
            AzureSearchConfiguration searchConfig,
            AzureOpenAIConfiguration openAIConfig)
        {
            _db = db;
            
            var searchCredential = new AzureKeyCredential(searchConfig.ApiKey);
            _indexClient = new SearchIndexClient(new Uri(searchConfig.Endpoint), searchCredential);
            _searchClient = _indexClient.GetSearchClient(searchConfig.IndexName);

            var openAICredential = new AzureKeyCredential(openAIConfig.ApiKey);
            _openAIClient = new AzureOpenAIClient(new Uri(openAIConfig.Endpoint), openAICredential);
            _embeddingDeployment = openAIConfig.EmbeddingDeployment;
        }

        public async Task InitializeIndexAsync(CancellationToken ct = default)
        {
            // Define vector search configuration
            var vectorSearch = new VectorSearch();
            vectorSearch.Profiles.Add(new VectorSearchProfile("vector-profile", "hnsw-config"));
            vectorSearch.Algorithms.Add(new HnswAlgorithmConfiguration("hnsw-config"));

            // Create index
            var index = new SearchIndex("products")
            {
                Fields = new FieldBuilder().Build(typeof(ProductSearchDocument)),
                VectorSearch = vectorSearch
            };

            await _indexClient.CreateOrUpdateIndexAsync(index, cancellationToken: ct);
        }

        public async Task IndexProductsAsync(CancellationToken ct = default)
        {
            var products = await _db.Products.Where(p => p.IsActive).ToListAsync(ct);
            var documents = new List<ProductSearchDocument>();

            foreach (var product in products)
            {
                // Generate embedding for product
                var textToEmbed = $"{product.Name} {product.Description} {product.Category}";
                var embedding = await GenerateEmbeddingAsync(textToEmbed, ct);

                documents.Add(new ProductSearchDocument
                {
                    Id = product.Id.ToString(),
                    Sku = product.Sku,
                    Name = product.Name,
                    Description = product.Description,
                    Category = product.Category,
                    Price = (double)product.Price,
                    IsActive = product.IsActive,
                    Embedding = embedding
                });
            }

            if (documents.Any())
            {
                await _searchClient.IndexDocumentsAsync(
                    IndexDocumentsBatch.Upload(documents),
                    cancellationToken: ct);
            }
        }

        public async Task<IEnumerable<Product>> SearchProductsAsync(
            string query, 
            int maxResults = 10, 
            CancellationToken ct = default)
        {
            // Generate embedding for search query
            var queryEmbedding = await GenerateEmbeddingAsync(query, ct);

            // Perform hybrid search (vector + keyword)
            var searchOptions = new SearchOptions
            {
                Size = maxResults,
                Select = { "Id", "Sku", "Name", "Description", "Category", "Price" }
            };

            searchOptions.VectorSearch = new VectorSearchOptions();
            searchOptions.VectorSearch.Queries.Add(new VectorizedQuery(new ReadOnlyMemory<float>(queryEmbedding.ToArray()))
            {
                KNearestNeighborsCount = maxResults,
                Fields = { "Embedding" }
            });

            var results = await _searchClient.SearchAsync<ProductSearchDocument>(
                query, 
                searchOptions, 
                cancellationToken: ct);

            // Convert to Product models
            var productIds = new List<int>();
            await foreach (var result in results.Value.GetResultsAsync())
            {
                if (int.TryParse(result.Document.Id, out var id))
                {
                    productIds.Add(id);
                }
            }

            // Fetch full products from database in the same order
            var products = await _db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync(ct);

            return productIds
                .Select(id => products.FirstOrDefault(p => p.Id == id))
                .Where(p => p != null)
                .Cast<Product>();
        }

        private async Task<IReadOnlyList<float>> GenerateEmbeddingAsync(
            string text, 
            CancellationToken ct = default)
        {
            var embeddingClient = _openAIClient.GetEmbeddingClient(_embeddingDeployment);
            var result = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: ct);
            return result.Value.ToFloats().ToArray();
        }
    }
}
```

### 3.5 Register Services in Program.cs

Add to `Program.cs` after existing service registrations:

```csharp
// Azure Search configuration
builder.Services.Configure<AzureOpenAIConfiguration>(
    builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.Configure<AzureSearchConfiguration>(
    builder.Configuration.GetSection("AzureSearch"));

builder.Services.AddSingleton(sp => 
    sp.GetRequiredService<IOptions<AzureOpenAIConfiguration>>().Value);
builder.Services.AddSingleton(sp => 
    sp.GetRequiredService<IOptions<AzureSearchConfiguration>>().Value);

builder.Services.AddScoped<ISearchService, SearchService>();
```

### 3.6 Update Products Page Model

**File**: `Pages/Products/Index.cshtml.cs`

Add search capability:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RetailMonolith.Data;
using RetailMonolith.Models;
using RetailMonolith.Services;

namespace RetailMonolith.Pages.Products
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ISearchService _searchService;

        public IndexModel(AppDbContext db, ISearchService searchService)
        {
            _db = db;
            _searchService = searchService;
        }

        public IEnumerable<Product> Products { get; set; } = [];

        [BindProperty(SupportsGet = true)]
        public string? SearchQuery { get; set; }

        public async Task OnGetAsync()
        {
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                // Use semantic search
                Products = await _searchService.SearchProductsAsync(SearchQuery);
            }
            else
            {
                // Show all products
                Products = await _db.Products.Where(p => p.IsActive).ToListAsync();
            }
        }
    }
}
```

### 3.7 Update Products Page UI

**File**: `Pages/Products/Index.cshtml`

Add search box at the top:

```html
@page
@model RetailMonolith.Pages.Products.IndexModel
@{
    ViewData["Title"] = "Products";
}

<div class="container mt-4">
    <h1>Products</h1>

    <!-- Semantic Search Box -->
    <div class="row mb-4">
        <div class="col-md-8 mx-auto">
            <form method="get" class="d-flex">
                <input 
                    type="text" 
                    name="SearchQuery" 
                    class="form-control me-2" 
                    placeholder="Search products naturally (e.g., 'comfortable running shoes')" 
                    value="@Model.SearchQuery" 
                    autofocus />
                <button type="submit" class="btn btn-primary">Search</button>
                @if (!string.IsNullOrWhiteSpace(Model.SearchQuery))
                {
                    <a href="/Products" class="btn btn-secondary ms-2">Clear</a>
                }
            </form>
        </div>
    </div>

    @if (!string.IsNullOrWhiteSpace(Model.SearchQuery))
    {
        <div class="alert alert-info">
            Showing results for: <strong>@Model.SearchQuery</strong>
        </div>
    }

    <!-- Product Grid (existing code) -->
    <div class="row">
        @foreach (var product in Model.Products)
        {
            <div class="col-md-4 mb-3">
                <!-- existing product card code -->
            </div>
        }
    </div>
</div>
```

---

## Step 4: Initialize and Index Products

Create a one-time setup endpoint or CLI command to initialize the search index.

### Option A: Add Admin Endpoint

Add to `Program.cs` before `app.Run()`:

```csharp
// Admin endpoint to initialize search
app.MapPost("/admin/search/initialize", async (ISearchService searchService) =>
{
    await searchService.InitializeIndexAsync();
    await searchService.IndexProductsAsync();
    return Results.Ok("Search index initialized and products indexed successfully");
});
```

Then call it once:
```bash
curl -X POST https://localhost:5001/admin/search/initialize
```

### Option B: Add to Startup (Auto-Initialize)

Add to `Program.cs` after database migration:

```csharp
// Auto-initialize search index on startup (for development)
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();
        await searchService.InitializeIndexAsync();
        await searchService.IndexProductsAsync();
    }
}
```

---

## Step 5: Testing

### 5.1 Test Search Queries

Navigate to `/Products` and try these searches:

- **"comfortable shoes for running"** → Should find athletic footwear
- **"gifts under 20 pounds"** → Price-filtered semantic search
- **"stylish accessories"** → Category-aware search
- **"electronics for home"** → Multi-category search

### 5.2 Monitor Azure AI Search

1. Go to Azure Portal → Your AI Search resource
2. Click "Search Explorer"
3. See indexed documents and test queries

### 5.3 Monitor Costs

- **Azure OpenAI**: ~$0.0001 per 1K tokens (embeddings)
- **Azure AI Search**: ~$75/month (Basic tier) or Free tier for testing

---

## Step 6: Production Considerations

### 6.1 Security
- **Use Managed Identity** instead of API keys in production
- Store secrets in **Azure Key Vault**
- Enable **RBAC** on Azure resources

### 6.2 Performance
- **Cache embeddings** for frequently searched queries
- Use **batch indexing** for bulk updates
- Consider **async indexing** when products change

### 6.3 Monitoring
- Enable **Application Insights** for search telemetry
- Track search queries and results quality
- Monitor API rate limits

### 6.4 Index Updates
- Trigger re-indexing when products are added/updated:

```csharp
// In your product update logic
await _searchService.IndexProductsAsync();
```

---

## Architecture Diagram

```
User Query: "comfortable running shoes"
        ↓
   Products Page
        ↓
   SearchService
        ↓
   Azure OpenAI (text-embedding-ada-002)
   → Generate query embedding [1536 dimensions]
        ↓
   Azure AI Search
   → Vector similarity search + keyword matching
   → Return top N results
        ↓
   AppDbContext
   → Fetch full product details
        ↓
   Display Results
```

---

## Cost Estimate

**For 50 products, ~1000 searches/month:**

- **Azure OpenAI**: ~$0.50/month (embeddings)
- **Azure AI Search Basic**: ~$75/month (or Free tier: $0)
- **Total**: ~$75.50/month (or $0.50 with Free tier)

**Free tier limitations:**
- 50 MB storage
- 3 indexes max
- No SLA

---

## Next Steps

1. ✅ Set up Azure resources
2. ✅ Install NuGet packages
3. ✅ Implement search service
4. ✅ Update Products page
5. ✅ Initialize search index
6. ✅ Test semantic search

**Enhancements:**
- Add search filters (price range, category)
- Implement search result ranking/boosting
- Add search analytics and telemetry
- Create admin UI for re-indexing
- Add autocomplete suggestions

---

## Troubleshooting

### "Index not found"
→ Run initialization endpoint or startup code

### "Quota exceeded"
→ Check Azure OpenAI rate limits, upgrade tier if needed

### "No results returned"
→ Verify products are indexed: Check Azure Portal → Search Explorer

### "Embedding dimensions mismatch"
→ Ensure `VectorSearchDimensions = 1536` for `text-embedding-ada-002`
→ Use `1536` for ada-002, `3072` for text-embedding-3-large

---

## References

- [Azure AI Search Documentation](https://learn.microsoft.com/azure/search/)
- [Azure OpenAI Service](https://learn.microsoft.com/azure/ai-services/openai/)
- [Vector Search in Azure AI Search](https://learn.microsoft.com/azure/search/vector-search-overview)
- [Text Embeddings](https://platform.openai.com/docs/guides/embeddings)

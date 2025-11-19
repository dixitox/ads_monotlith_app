# Add Semantic Search for Natural Language Product Discovery

## Overview
Implement semantic search functionality to enable natural-language product discovery using Azure OpenAI embeddings and Azure AI Search. This will allow users to search for products using conversational queries like "comfortable shoes for running" instead of exact keyword matching.

## Prerequisites
- Azure subscription with access to Azure OpenAI and Azure AI Search
- See [SEMANTIC_SEARCH_GUIDE.md](./SEMANTIC_SEARCH_GUIDE.md) for detailed Azure setup instructions

## Technical Requirements

### 1. NuGet Packages
Add the following packages to `RetailMonolith.csproj`:
```xml
<PackageReference Include="Azure.Search.Documents" Version="11.6.0" />
<PackageReference Include="Azure.AI.OpenAI" Version="2.1.0" />
```

### 2. Configuration

#### GitHub Secrets Setup
Store Azure credentials as GitHub repository secrets:

1. Go to your repository: `https://github.com/adrianlavery/ads_monolith_app`
2. Navigate to **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret** and add the following:

| Secret Name | Value | Description |
|-------------|-------|-------------|
| `AZURE_OPENAI_ENDPOINT` | `https://[your-resource].openai.azure.com/` | Azure OpenAI endpoint URL |
| `AZURE_OPENAI_API_KEY` | `sk-...` | Azure OpenAI API key |
| `AZURE_SEARCH_ENDPOINT` | `https://[your-search].search.windows.net` | Azure AI Search endpoint URL |
| `AZURE_SEARCH_API_KEY` | `...` | Azure AI Search admin key |

#### Application Configuration
Update `appsettings.json` (no secrets here):
```json
{
  "AzureOpenAI": {
    "Endpoint": "",
    "ApiKey": "",
    "EmbeddingDeployment": "text-embedding-ada-002"
  },
  "AzureSearch": {
    "Endpoint": "",
    "ApiKey": "",
    "IndexName": "products"
  }
}
```

For local development, add to `appsettings.Development.json` (this file should be in `.gitignore`):
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://[your-resource].openai.azure.com/",
    "ApiKey": "your-local-dev-key",
    "EmbeddingDeployment": "text-embedding-ada-002"
  },
  "AzureSearch": {
    "Endpoint": "https://[your-search].search.windows.net",
    "ApiKey": "your-local-dev-key",
    "IndexName": "products"
  }
}
```

## Implementation Tasks

### Task 1: Create Configuration Models
**File**: `Models/SearchConfiguration.cs`

Create configuration POCOs:
- `AzureOpenAIConfiguration` (Endpoint, ApiKey, EmbeddingDeployment)
- `AzureSearchConfiguration` (Endpoint, ApiKey, IndexName)

### Task 2: Create Search Index Model
**File**: `Models/ProductSearchDocument.cs`

Define Azure AI Search document model with:
- Standard product fields (Id, Sku, Name, Description, Category, Price, IsActive)
- Vector field for embeddings (1536 dimensions for ada-002)
- Appropriate search attributes ([SearchableField], [SimpleField], etc.)

### Task 3: Create Search Service Interface
**File**: `Services/ISearchService.cs`

Define interface with methods:
```csharp
Task InitializeIndexAsync(CancellationToken ct = default);
Task IndexProductsAsync(CancellationToken ct = default);
Task<IEnumerable<Product>> SearchProductsAsync(string query, int maxResults = 10, CancellationToken ct = default);
```

### Task 4: Implement Search Service
**File**: `Services/SearchService.cs`

Implement `ISearchService` with:
- **InitializeIndexAsync**: Create/update Azure AI Search index with vector search configuration
- **IndexProductsAsync**: 
  - Fetch all active products from database
  - Generate embeddings for each product (concatenate name, description, category)
  - Upload documents to Azure AI Search
- **SearchProductsAsync**:
  - Generate embedding for user query
  - Perform hybrid search (vector + keyword)
  - Return matching products ordered by relevance
- Helper method `GenerateEmbeddingAsync`: Call Azure OpenAI to get embeddings

**Key Implementation Details**:
- Use HNSW algorithm for vector search
- Configure vector profile with 1536 dimensions
- Implement proper error handling and cancellation token support
- Return results maintaining search ranking order

### Task 5: Register Services
**File**: `Program.cs`

Add after existing service registrations:
```csharp
// Configure Azure Search settings (with environment variable override for GitHub secrets)
builder.Services.Configure<AzureOpenAIConfiguration>(options =>
{
    var config = builder.Configuration.GetSection("AzureOpenAI");
    options.Endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? config["Endpoint"] ?? "";
    options.ApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ?? config["ApiKey"] ?? "";
    options.EmbeddingDeployment = config["EmbeddingDeployment"] ?? "text-embedding-ada-002";
});

builder.Services.Configure<AzureSearchConfiguration>(options =>
{
    var config = builder.Configuration.GetSection("AzureSearch");
    options.Endpoint = Environment.GetEnvironmentVariable("AZURE_SEARCH_ENDPOINT") ?? config["Endpoint"] ?? "";
    options.ApiKey = Environment.GetEnvironmentVariable("AZURE_SEARCH_API_KEY") ?? config["ApiKey"] ?? "";
    options.IndexName = config["IndexName"] ?? "products";
});

builder.Services.AddSingleton(sp => 
    sp.GetRequiredService<IOptions<AzureOpenAIConfiguration>>().Value);
builder.Services.AddSingleton(sp => 
    sp.GetRequiredService<IOptions<AzureSearchConfiguration>>().Value);

builder.Services.AddScoped<ISearchService, SearchService>();
```

Add initialization endpoint before `app.Run()`:
```csharp
app.MapPost("/admin/search/initialize", async (ISearchService searchService) =>
{
    await searchService.InitializeIndexAsync();
    await searchService.IndexProductsAsync();
    return Results.Ok(new { message = "Search index initialized successfully" });
}).WithName("InitializeSearch");
```

### Task 6: Update Products Page Model
**File**: `Pages/Products/Index.cshtml.cs`

Modifications:
- Inject `ISearchService` via constructor
- Add `SearchQuery` property with `[BindProperty(SupportsGet = true)]`
- Update `OnGetAsync()` to:
  - If `SearchQuery` is provided → use `SearchProductsAsync()`
  - Otherwise → show all active products (existing logic)

### Task 7: Update Products Page UI
**File**: `Pages/Products/Index.cshtml`

Add search interface before product grid:
- Search input box with placeholder: "Search products naturally (e.g., 'comfortable running shoes')"
- Search button
- Clear button (shown only when search is active)
- Search results indicator showing current query

Maintain existing product grid display logic.

## Testing Checklist

### Local Development Testing
- [ ] Configure credentials in `appsettings.Development.json`
- [ ] Verify `appsettings.Development.json` is in `.gitignore`
- [ ] Search index initializes successfully via `/admin/search/initialize`
- [ ] Products are indexed with embeddings
- [ ] Natural language queries return relevant results:
  - [ ] "comfortable running shoes" → athletic footwear
  - [ ] "gifts under 20 pounds" → low-priced items
  - [ ] "stylish accessories" → accessories category
  - [ ] "electronics for home" → home electronics
- [ ] Empty search shows all products (existing behavior)
- [ ] Clear button resets to all products

### GitHub Secrets & Deployment Testing
- [ ] All 4 secrets configured in GitHub repository settings
- [ ] Environment variables properly read in application startup
- [ ] No secrets committed to repository (verify with `git log` and file review)
- [ ] Application runs successfully with environment variables
- [ ] No errors in Azure AI Search portal
- [ ] Application Insights shows successful OpenAI calls (if configured)

## Performance Considerations

- Embeddings are generated once per product during indexing
- Query embeddings generated on-demand (consider caching for frequent queries)
- Azure AI Search handles vector similarity computation efficiently
- Batch product indexing to avoid rate limits

## Security Notes

⚠️ **Important**: 
- **GitHub Secrets**: All Azure credentials stored as repository secrets
- **Environment Variables**: Application reads from `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY`, `AZURE_SEARCH_ENDPOINT`, `AZURE_SEARCH_API_KEY`
- **Local Development**: Use `appsettings.Development.json` (add to `.gitignore`)
- **Production**: Consider upgrading to Azure Key Vault or Managed Identity
- **Never commit** API keys or secrets to the repository

### Setting up .gitignore
Ensure `.gitignore` contains:
```
appsettings.Development.json
*.user
```

## Cost Estimate

For 50 products with ~1000 searches/month:
- Azure OpenAI embeddings: ~$0.50/month
- Azure AI Search Basic tier: ~$75/month (or Free tier for testing)

## Documentation

See [SEMANTIC_SEARCH_GUIDE.md](./SEMANTIC_SEARCH_GUIDE.md) for:
- Detailed Azure resource setup steps
- Configuration examples
- Architecture diagrams
- Troubleshooting guide
- Production deployment considerations

## Acceptance Criteria

- ✅ Users can search products using natural language queries
- ✅ Search returns semantically relevant results
- ✅ Hybrid search combines vector similarity with keyword matching
- ✅ Search maintains good performance (<2s response time)
- ✅ Code follows existing project patterns and conventions
- ✅ Configuration properly externalized
- ✅ Error handling implemented
- ✅ No sensitive data committed to repository

## Related Issues

- Potential follow-ups:
  - Add search filters (price range, category facets)
  - Implement search analytics/telemetry
  - Add autocomplete/suggestions
  - Create admin UI for re-indexing products

---

**Implementation Reference**: All code examples and detailed steps are in `SEMANTIC_SEARCH_GUIDE.md`

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailDecomposed.Services;

namespace RetailDecomposed.Pages.Search;

public class IndexModel : PageModel
{
    private readonly ISemanticSearchService _searchService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ISemanticSearchService searchService,
        ILogger<IndexModel> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    public string? SearchQuery { get; set; }
    public string? SelectedCategory { get; set; }
    public List<ProductSearchResult> SearchResults { get; set; } = new();
    public bool HasSearched { get; set; }
    public bool IsIndexing { get; set; }
    public string? ErrorMessage { get; set; }
    public string[] Categories { get; } = new[] { "All", "Beauty", "Apparel", "Footwear", "Home", "Accessories", "Electronics" };

    public void OnGet()
    {
        // Initial page load - no search performed
    }

    public async Task OnGetSearchAsync(string? query, string? category)
    {
        HasSearched = true;
        SearchQuery = query;
        SelectedCategory = category;

        if (string.IsNullOrWhiteSpace(query))
        {
            ErrorMessage = "Please enter a search query.";
            return;
        }

        try
        {
            _logger.LogInformation("Performing search for: {Query}, Category: {Category}", query, category);

            var categoryFilter = !string.IsNullOrWhiteSpace(category) && category != "All" ? category : null;
            SearchResults = await _searchService.SearchProductsAsync(query, top: 20, categoryFilter: categoryFilter);

            _logger.LogInformation("Search returned {Count} results", SearchResults.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing search");
            ErrorMessage = "An error occurred while searching. Please ensure the search index has been created and products have been indexed.";
        }
    }

    public async Task<IActionResult> OnPostCreateIndexAsync()
    {
        // Authorization check - only Admins can create index
        if (!User.IsInRole("Admin"))
        {
            _logger.LogWarning("⚠️ Unauthorized index creation attempt by user: {User}", User.Identity?.Name ?? "Unknown");
            ErrorMessage = "❌ Access denied. Only administrators can create the search index.";
            return Page();
        }

        try
        {
            _logger.LogInformation("Creating search index by admin: {User}", User.Identity?.Name);
            var success = await _searchService.CreateOrUpdateIndexAsync();
            
            if (success)
            {
                _logger.LogInformation("✅ Search index created successfully");
                TempData["SuccessMessage"] = "✅ Search index created successfully! Now you can index products.";
            }
            else
            {
                ErrorMessage = "❌ Failed to create search index.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error creating search index");
            ErrorMessage = $"❌ Error creating search index: {ex.Message}";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostIndexProductsAsync()
    {
        // Authorization check - only Admins can index products
        if (!User.IsInRole("Admin"))
        {
            _logger.LogWarning("⚠️ Unauthorized index attempt by user: {User}", User.Identity?.Name ?? "Unknown");
            ErrorMessage = "❌ Access denied. Only administrators can index products.";
            return Page();
        }

        try
        {
            IsIndexing = true;
            _logger.LogInformation("Starting product indexing by admin: {User}", User.Identity?.Name);
            
            var count = await _searchService.IndexProductsAsync();
            
            if (count > 0)
            {
                _logger.LogInformation("✅ Successfully indexed {Count} products", count);
                TempData["SuccessMessage"] = $"✅ Successfully indexed {count} products with embeddings!";
            }
            else
            {
                _logger.LogWarning("⚠️ No products were indexed");
                ErrorMessage = "⚠️ No products were indexed. Check the logs for details.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error indexing products");
            ErrorMessage = $"❌ Error indexing products: {ex.Message}. Check console logs for details.";
        }

        return Page();
    }
}

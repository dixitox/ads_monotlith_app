using Microsoft.AspNetCore.Mvc;
using RetailDecomposed.Services;

namespace RetailDecomposed.Controllers;

/// <summary>
/// API Controller for semantic search operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ISemanticSearchService _searchService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        ISemanticSearchService searchService,
        ILogger<SearchController> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// Creates or updates the Azure AI Search index.
    /// </summary>
    /// <returns>Success status.</returns>
    [HttpPost("create-index")]
    public async Task<IActionResult> CreateIndex()
    {
        try
        {
            _logger.LogInformation("Creating search index");
            var success = await _searchService.CreateOrUpdateIndexAsync();
            
            if (success)
            {
                return Ok(new { message = "Search index created or updated successfully" });
            }
            
            return StatusCode(500, new { message = "Failed to create or update search index" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating search index");
            return StatusCode(500, new { message = "Error creating search index", error = ex.Message });
        }
    }

    /// <summary>
    /// Indexes all products from the database into Azure AI Search with embeddings.
    /// </summary>
    /// <returns>Number of products indexed.</returns>
    [HttpPost("index")]
    public async Task<IActionResult> IndexProducts()
    {
        try
        {
            _logger.LogInformation("Starting product indexing");
            var count = await _searchService.IndexProductsAsync();
            
            return Ok(new { message = $"Successfully indexed {count} products", count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing products");
            return StatusCode(500, new { message = "Error indexing products", error = ex.Message });
        }
    }

    /// <summary>
    /// Performs semantic search on products using natural language query.
    /// </summary>
    /// <param name="query">Natural language search query.</param>
    /// <param name="top">Maximum number of results to return (default: 10).</param>
    /// <param name="category">Optional category filter.</param>
    /// <returns>List of matching products with relevance scores.</returns>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string query,
        [FromQuery] int top = 10,
        [FromQuery] string? category = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { message = "Query parameter is required" });
            }

            if (top < 1 || top > 100)
            {
                return BadRequest(new { message = "Top parameter must be between 1 and 100" });
            }

            _logger.LogInformation("Searching for: {Query} (top: {Top}, category: {Category})", 
                query, top, category ?? "all");

            var results = await _searchService.SearchProductsAsync(query, top, category);
            
            return Ok(new 
            { 
                query,
                count = results.Count,
                results 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing search");
            return StatusCode(500, new { message = "Error performing search", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets available product categories for filtering.
    /// </summary>
    /// <returns>List of categories.</returns>
    [HttpGet("categories")]
    public IActionResult GetCategories()
    {
        var categories = new[] { "Beauty", "Apparel", "Footwear", "Home", "Accessories", "Electronics" };
        return Ok(new { categories });
    }
}

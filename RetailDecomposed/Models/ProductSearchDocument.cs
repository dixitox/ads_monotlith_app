using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace RetailDecomposed.Models;

/// <summary>
/// Represents a product document in Azure AI Search with vector embeddings for semantic search.
/// </summary>
public class ProductSearchDocument
{
    /// <summary>
    /// Unique identifier for the product (must be a string for Azure AI Search).
    /// </summary>
    [SimpleField(IsKey = true, IsFilterable = true)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Product SKU for identification and filtering.
    /// </summary>
    [SimpleField(IsFilterable = true, IsSortable = true)]
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// Product name - searchable and used for keyword matching.
    /// </summary>
    [SearchableField(IsFilterable = true, IsSortable = true)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Product description - searchable for full-text search.
    /// </summary>
    [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.EnMicrosoft)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Product category for filtering and faceted navigation.
    /// </summary>
    [SearchableField(IsFilterable = true, IsFacetable = true)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Product price for filtering, sorting, and display.
    /// </summary>
    [SimpleField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
    public decimal Price { get; set; }

    /// <summary>
    /// Vector embeddings for semantic search combining name and description.
    /// 1536 dimensions for text-embedding-3-small model.
    /// </summary>
    [VectorSearchField(VectorSearchDimensions = 1536, VectorSearchProfileName = "vector-profile")]
    public ReadOnlyMemory<float>? NameDescriptionVector { get; set; }
}

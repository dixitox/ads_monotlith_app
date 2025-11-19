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

        [VectorSearchField(VectorSearchDimensions = 1536, VectorSearchProfileName = "vector-profile")]
        public IReadOnlyList<float>? Embedding { get; set; }
    }
}

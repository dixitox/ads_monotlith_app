namespace RetailDecomposed.Constants;

/// <summary>
/// Defines the valid product categories used throughout the application.
/// This is the single source of truth for category values used in search filters,
/// UI dropdowns, and validation.
/// </summary>
public static class ProductCategories
{
    /// <summary>
    /// Array of all valid product categories.
    /// Used for category filtering in search and validation.
    /// </summary>
    public static readonly string[] All = new[] 
    { 
        "Beauty", 
        "Apparel", 
        "Footwear", 
        "Home", 
        "Accessories", 
        "Electronics" 
    };

    /// <summary>
    /// Validates if the provided category is in the valid categories list.
    /// </summary>
    /// <param name="category">Category name to validate.</param>
    /// <returns>True if the category is valid, false otherwise.</returns>
    public static bool IsValid(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return false;
            
        return All.Contains(category, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the properly cased category name if valid, null otherwise.
    /// Also sanitizes the input to prevent OData injection by escaping single quotes.
    /// </summary>
    /// <param name="category">Category name to normalize.</param>
    /// <returns>Properly cased and sanitized category name, or null if invalid.</returns>
    public static string? GetNormalizedCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return null;
            
        return All.FirstOrDefault(c => c.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Escapes single quotes in a string for safe use in OData filter expressions.
    /// </summary>
    /// <param name="value">Value to escape.</param>
    /// <returns>Escaped value safe for OData filters.</returns>
    public static string EscapeForOData(string value)
    {
        // OData spec: single quotes in string literals must be escaped by doubling them
        return value.Replace("'", "''");
    }
}

using System.Diagnostics;

namespace RetailDecomposed.Services;

/// <summary>
/// Centralized management of ActivitySources for distributed tracing.
/// Each ActivitySource represents a logical component of the application.
/// </summary>
public static class TelemetryActivitySources
{
    public const string ServiceName = "RetailDecomposed";
    
    /// <summary>
    /// ActivitySource for AI Copilot operations
    /// </summary>
    public static readonly ActivitySource Copilot = new(
        $"{ServiceName}.Services.Copilot",
        "1.0.0");
    
    /// <summary>
    /// ActivitySource for Products API operations
    /// </summary>
    public static readonly ActivitySource Products = new(
        $"{ServiceName}.Services.Products",
        "1.0.0");
    
    /// <summary>
    /// ActivitySource for Cart API operations
    /// </summary>
    public static readonly ActivitySource Cart = new(
        $"{ServiceName}.Services.Cart",
        "1.0.0");
    
    /// <summary>
    /// ActivitySource for Orders API operations
    /// </summary>
    public static readonly ActivitySource Orders = new(
        $"{ServiceName}.Services.Orders",
        "1.0.0");
    
    /// <summary>
    /// ActivitySource for Checkout API operations
    /// </summary>
    public static readonly ActivitySource Checkout = new(
        $"{ServiceName}.Services.Checkout",
        "1.0.0");
}

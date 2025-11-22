using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RetailMonolith.Data;
using RetailMonolith.Models;
using RetailDecomposed.Services;

namespace RetailMonolith.Pages.Products
{
    [Authorize(Policy = "CustomerAccess")]
    public class DetailsModel : PageModel
    {
        private readonly IProductsApiClient _productsApiClient;
        private readonly ICartApiClient _cartApiClient;
        private readonly AppDbContext _db;

        public DetailsModel(IProductsApiClient productsApiClient, ICartApiClient cartApiClient, AppDbContext db)
        {
            _productsApiClient = productsApiClient;
            _cartApiClient = cartApiClient;
            _db = db;
        }

        public Product? Product { get; set; }
        public int InventoryQuantity { get; set; }

        // Category → Image mapping (same as Index page)
        public static readonly Dictionary<string, string> CategoryImages = new()
        {
            ["Beauty"] = "https://images.unsplash.com/photo-1596462502278-27bfdc403348?auto=format&fit=crop&w=800&q=80",
            ["Apparel"] = "https://images.unsplash.com/photo-1489987707025-afc232f7ea0f?auto=format&fit=crop&w=800&q=80",
            ["Footwear"] = "https://images.unsplash.com/photo-1603808033192-082d6919d3e1?auto=format&fit=crop&w=800&q=80",
            ["Home"] = "https://images.unsplash.com/photo-1583847268964-b28dc8f51f92?auto=format&fit=crop&w=800&q=80",
            ["Accessories"] = "https://images.unsplash.com/photo-1586878341523-7acb55eb8c12?auto=format&fit=crop&w=800&q=80",
            ["Electronics"] = "https://images.unsplash.com/photo-1498049794561-7780e7231661?auto=format&fit=crop&w=800&q=80"
        };

        public string GetImageForCategory(string? category)
        {
            if (CategoryImages.TryGetValue(category ?? string.Empty, out var url))
                return url;

            // Fallback image if category missing
            return "https://images.unsplash.com/photo-1526170375885-4d8ecf77b99f?auto=format&fit=crop&w=800&q=80";
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            try
            {
                // Fetch product from API client
                Product = await _productsApiClient.GetProductByIdAsync(id);

                if (Product == null)
                {
                    return NotFound();
                }

                // Get inventory quantity
                var inventoryItem = await _db.Inventory
                    .FirstOrDefaultAsync(i => i.Sku == Product.Sku);
                
                InventoryQuantity = inventoryItem?.Quantity ?? 0;

                return Page();
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            try
            {
                var customerId = User.Identity?.Name ?? "guest";
                
                // Get product details
                Product = await _productsApiClient.GetProductByIdAsync(id);
                
                if (Product == null)
                {
                    TempData["Error"] = "Product not found.";
                    return RedirectToPage("/Products/Index");
                }

                // Add to cart via API client
                await _cartApiClient.AddToCartAsync(customerId, id, 1);

                TempData["Success"] = $"{Product.Name} has been added to your cart.";
                return RedirectToPage("/Cart/Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to add product to cart: {ex.Message}";
                return RedirectToPage("/Products/Details", new { id });
            }
        }
    }
}

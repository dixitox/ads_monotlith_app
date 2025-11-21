using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailDecomposed.Services;

namespace RetailMonolith.Pages.Cart
{
    [Authorize(Policy = "CustomerAccess")]
    public class IndexModel : PageModel
    {
        private readonly ICartApiClient _cartApiClient;
       
        public IndexModel(ICartApiClient cartApiClient)
        {
            _cartApiClient = cartApiClient;
        }

        // Decomposed: Cart data now comes from the Cart API instead of direct database access
        public List<(string Name, int Quantity, decimal Price)> Lines { get; set; } = new(); 

        public decimal Total => Lines.Sum(line => line.Price * line.Quantity);

        public async Task OnGetAsync()
        {
            // Call the Cart API instead of using ICartService directly
            var cart = await _cartApiClient.GetCartAsync("guest");
            Lines = cart.Lines
                .Select(line => (line.Name, line.Quantity, line.UnitPrice))
                .ToList();
        }
    }
}

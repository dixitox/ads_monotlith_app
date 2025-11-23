using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using RetailDecomposed.Services;
using System.Threading.Tasks;

namespace RetailMonolith.Pages.Checkout
{
    [Authorize(Policy = "CustomerAccess")]
    public class IndexModel : PageModel
    {
        private readonly ICartApiClient _cartApiClient;
        private readonly ICheckoutApiClient _checkoutApiClient;
        
        public IndexModel(ICartApiClient cartApiClient, ICheckoutApiClient checkoutApiClient)
        {
            _cartApiClient = cartApiClient;
            _checkoutApiClient = checkoutApiClient;
        }

        // For simplicity, using a hardcoded customer ID
        // In a real application, this would come from the authenticated user context
        // or session  
        public List<(string Name, int Qty, decimal Price)> Lines { get; set; } = new();

        public decimal Total => Lines.Sum(l => l.Price * l.Qty);

        [BindProperty]
        public string PaymentToken { get; set; } = "tok_test";

        public async Task OnGetAsync()
        {
            // Decomposed: Fetch cart from Cart API
            // Use authenticated user's identity as customerId
            var customerId = User.Identity?.Name ?? "guest";
            var cart = await _cartApiClient.GetCartAsync(customerId);
            Lines = cart.Lines
                .Select(line => (line.Name, line.Quantity, line.UnitPrice))
                .ToList();
        }

        public async Task<IActionResult> OnPostAsync()
        {
           if(!ModelState.IsValid)
           {
                await OnGetAsync();
                return Page();
            }

            // Decomposed: Checkout via Checkout API instead of direct service call
            // Use authenticated user's identity as customerId
            var customerId = User.Identity?.Name ?? "guest";
            var order = await _checkoutApiClient.CheckoutAsync(customerId, PaymentToken);

            // redirect to order confirmation page
            return Redirect($"/Orders/Details?id={order.Id}");
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailMonolith.Services;
using RetailDecomposed.Services;
using System.Threading.Tasks;

namespace RetailMonolith.Pages.Checkout
{
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
            var cart = await _cartApiClient.GetCartAsync("guest");
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
            var order = await _checkoutApiClient.CheckoutAsync("guest", PaymentToken);

            // redirect to order confirmation page
            return Redirect($"/Orders/Details?id={order.Id}");
        }
    }
}

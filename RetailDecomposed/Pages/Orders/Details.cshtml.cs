using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RetailDecomposed.Data;
using RetailDecomposed.Models;
using RetailDecomposed.Services;

namespace RetailMonolith.Pages.Orders
{
    [Authorize(Policy = "CustomerAccess")]
    public class DetailsModel : PageModel
    {
        private readonly IOrdersApiClient _ordersApiClient;
        public DetailsModel(IOrdersApiClient ordersApiClient)
        {
            _ordersApiClient = ordersApiClient;
        }

        public Order? Order { get; set; }
        
        // Decomposed: Order details now come from the Orders API instead of direct database access
        public async Task<IActionResult> OnGetAsync(int id)
        {
            Order = await _ordersApiClient.GetOrderByIdAsync(id);
            
            if (Order == null)
            {
                return NotFound();
            }
            
            // Verify ownership: users can only view their own orders unless they're admin
            var customerId = User.Identity?.Name ?? "guest";
            var isAdmin = User.IsInRole("Admin");
            
            if (!isAdmin && Order.CustomerId != customerId)
            {
                return Forbid();
            }
            
            return Page();
        }
    }
}

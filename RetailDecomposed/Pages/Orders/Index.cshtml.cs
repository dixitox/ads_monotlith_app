using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RetailMonolith.Data;
using RetailMonolith.Models;
using RetailDecomposed.Services;

namespace RetailMonolith.Pages.Orders
{
    [Authorize(Policy = "CustomerAccess")]
    public class IndexModel : PageModel
    {
        private readonly IOrdersApiClient _ordersApiClient;
        public IndexModel(IOrdersApiClient ordersApiClient)
        {
            _ordersApiClient = ordersApiClient;
        }

        public IList<Order> Orders { get; set; } = new List<Order>();
        public bool IsAdmin { get; set; }

        // Decomposed: Orders now come from the Orders API instead of direct database access
        public async Task OnGetAsync()
        {
            IsAdmin = User.IsInRole("Admin");
            var allOrders = await _ordersApiClient.GetOrdersAsync();
            
            // Admins see all orders, regular users see only their own
            if (IsAdmin)
            {
                Orders = allOrders;
            }
            else
            {
                var customerId = User.Identity?.Name ?? "guest";
                Orders = allOrders.Where(o => o.CustomerId == customerId).ToList();
            }
        }
    }
}

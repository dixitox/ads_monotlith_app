using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RetailMonolith.Data;
using RetailMonolith.Models;
using RetailDecomposed.Services;

namespace RetailMonolith.Pages.Orders
{
    [Authorize(Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly IOrdersApiClient _ordersApiClient;
        public IndexModel(IOrdersApiClient ordersApiClient)
        {
            _ordersApiClient = ordersApiClient;
        }

        public IList<Order> Orders { get; set; } = new List<Order>();

        // Decomposed: Orders now come from the Orders API instead of direct database access
        public async Task OnGetAsync() => Orders = await _ordersApiClient.GetOrdersAsync();
    }
}

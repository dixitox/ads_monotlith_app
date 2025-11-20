using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RetailMonolith.Data;
using RetailMonolith.Models;
using RetailDecomposed.Services;

namespace RetailMonolith.Pages.Orders
{
    public class DetailsModel : PageModel
    {
        private readonly IOrdersApiClient _ordersApiClient;
        public DetailsModel(IOrdersApiClient ordersApiClient)
        {
            _ordersApiClient = ordersApiClient;
        }

        public Order? Order { get; set; }
        
        // Decomposed: Order details now come from the Orders API instead of direct database access
        public async Task OnGetAsync(int id) => Order = await _ordersApiClient.GetOrderByIdAsync(id);
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailMonolith.Services;
using System.Text.Json;
using System.Text;

namespace RetailMonolith.Pages.Checkout
{
    public class IndexModel : PageModel
    {
        private readonly ICartService _cartService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<IndexModel> _logger;
        
        public IndexModel(ICartService cartService, IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<IndexModel> logger)
        {
            _cartService = cartService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
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
            var cart = await _cartService.GetCartWithLinesAsync("guest");
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

            try
            {
                // Call Checkout API instead of local service
                var httpClient = _httpClientFactory.CreateClient("CheckoutApi");
                
                var checkoutRequest = new
                {
                    customerId = "guest",
                    paymentToken = PaymentToken,
                    cartId = 0
                };

                var json = JsonSerializer.Serialize(checkoutRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation("Calling Checkout API for customer: guest");

                var response = await httpClient.PostAsync("/api/checkout", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<CheckoutApiResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result != null)
                    {
                        _logger.LogInformation("Checkout completed successfully. OrderId: {OrderId}", result.OrderId);
                        return Redirect($"/Orders/Details?id={result.OrderId}");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Checkout API returned error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    ModelState.AddModelError(string.Empty, "Checkout failed. Please try again.");
                    await OnGetAsync();
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Checkout API");
                ModelState.AddModelError(string.Empty, "Unable to process checkout. Please try again later.");
                await OnGetAsync();
                return Page();
            }

            ModelState.AddModelError(string.Empty, "Checkout failed. Please try again.");
            await OnGetAsync();
            return Page();
        }

        private class CheckoutApiResponse
        {
            public int OrderId { get; set; }
            public string Status { get; set; } = default!;
            public decimal Total { get; set; }
            public DateTime CreatedUtc { get; set; }
        }
    }
}

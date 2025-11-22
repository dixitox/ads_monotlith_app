using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RetailDecomposed.Pages.Copilot
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            _logger.LogInformation("AI Copilot page accessed");
        }
    }
}

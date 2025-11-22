using System.Net;

namespace RetailDecomposed.Services
{
    /// <summary>
    /// Propagates authentication cookies from the current HttpContext to outgoing HTTP requests.
    /// This allows authenticated Razor Pages to call protected API endpoints within the same app.
    /// </summary>
    public class CookiePropagatingHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CookiePropagatingHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            
            if (httpContext != null)
            {
                // Get all cookies from the current request
                var cookieHeader = httpContext.Request.Headers.Cookie.ToString();
                
                if (!string.IsNullOrEmpty(cookieHeader))
                {
                    // Add the cookies to the outgoing request
                    request.Headers.Add("Cookie", cookieHeader);
                }
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}

using Microsoft.AspNetCore.Http;

namespace RetailDecomposed.Tests;

/// <summary>
/// DelegatingHandler that propagates authentication headers from the current HTTP context
/// to outgoing HTTP requests. This is used in tests to ensure API-to-API calls include
/// the authentication context.
/// </summary>
public class AuthenticationPropagatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthenticationPropagatingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext != null)
        {
            // Propagate authentication headers from the incoming request to the outgoing request
            if (httpContext.Request.Headers.TryGetValue(FakeAuthenticationHandler.UserIdHeader, out var userId))
            {
                request.Headers.TryAddWithoutValidation(FakeAuthenticationHandler.UserIdHeader, userId.ToString());
            }

            if (httpContext.Request.Headers.TryGetValue(FakeAuthenticationHandler.UserNameHeader, out var userName))
            {
                request.Headers.TryAddWithoutValidation(FakeAuthenticationHandler.UserNameHeader, userName.ToString());
            }

            if (httpContext.Request.Headers.TryGetValue(FakeAuthenticationHandler.UserEmailHeader, out var userEmail))
            {
                request.Headers.TryAddWithoutValidation(FakeAuthenticationHandler.UserEmailHeader, userEmail.ToString());
            }

            if (httpContext.Request.Headers.TryGetValue(FakeAuthenticationHandler.UserRolesHeader, out var userRoles))
            {
                request.Headers.TryAddWithoutValidation(FakeAuthenticationHandler.UserRolesHeader, userRoles.ToString());
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

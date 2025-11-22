using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace RetailDecomposed.Tests;

/// <summary>
/// Fake authentication handler for testing authenticated scenarios.
/// Allows tests to specify claims via request headers.
/// </summary>
public class FakeAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "FakeAuthentication";
    public const string UserIdHeader = "X-Test-UserId";
    public const string UserNameHeader = "X-Test-UserName";
    public const string UserEmailHeader = "X-Test-UserEmail";
    public const string UserRolesHeader = "X-Test-UserRoles";

    public FakeAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check if authentication is requested via headers
        if (!Request.Headers.ContainsKey(UserIdHeader))
        {
            // No authentication requested - return success with no claims (anonymous)
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Build claims from headers
        var claims = new List<Claim>();

        if (Request.Headers.TryGetValue(UserIdHeader, out var userId))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        }

        if (Request.Headers.TryGetValue(UserNameHeader, out var userName))
        {
            claims.Add(new Claim(ClaimTypes.Name, userName.ToString()));
        }

        if (Request.Headers.TryGetValue(UserEmailHeader, out var userEmail))
        {
            claims.Add(new Claim(ClaimTypes.Email, userEmail.ToString()));
        }

        if (Request.Headers.TryGetValue(UserRolesHeader, out var userRolesValue))
        {
            var roles = userRolesValue.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var role in roles)
            {
                // Use the same role claim type as Azure AD uses in production
                claims.Add(new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", role.Trim()));
            }
        }

        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

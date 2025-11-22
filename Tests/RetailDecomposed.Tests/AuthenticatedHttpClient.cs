using System.Net.Http.Headers;

namespace RetailDecomposed.Tests;

/// <summary>
/// Extension methods for HttpClient to add authentication headers for testing.
/// </summary>
public static class AuthenticatedHttpClientExtensions
{
    /// <summary>
    /// Configures the HttpClient to authenticate as a specific user.
    /// </summary>
    public static HttpClient AuthenticateAs(this HttpClient client, string userId, string userName, string? email = null, params string[] roles)
    {
        client.DefaultRequestHeaders.Add(FakeAuthenticationHandler.UserIdHeader, userId);
        client.DefaultRequestHeaders.Add(FakeAuthenticationHandler.UserNameHeader, userName);
        
        if (!string.IsNullOrEmpty(email))
        {
            client.DefaultRequestHeaders.Add(FakeAuthenticationHandler.UserEmailHeader, email);
        }

        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add(FakeAuthenticationHandler.UserRolesHeader, string.Join(",", roles));
        }

        return client;
    }

    /// <summary>
    /// Configures the HttpClient to authenticate as a customer (default test user).
    /// </summary>
    public static HttpClient AuthenticateAsCustomer(this HttpClient client)
    {
        return client.AuthenticateAs("test-user-1", "testuser@example.com", "testuser@example.com");
    }

    /// <summary>
    /// Configures the HttpClient to authenticate as an admin user.
    /// </summary>
    public static HttpClient AuthenticateAsAdmin(this HttpClient client)
    {
        return client.AuthenticateAs("admin-user-1", "admin@example.com", "admin@example.com", "Admin");
    }

    /// <summary>
    /// Removes all authentication headers to test as anonymous user.
    /// </summary>
    public static HttpClient AsAnonymous(this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove(FakeAuthenticationHandler.UserIdHeader);
        client.DefaultRequestHeaders.Remove(FakeAuthenticationHandler.UserNameHeader);
        client.DefaultRequestHeaders.Remove(FakeAuthenticationHandler.UserEmailHeader);
        client.DefaultRequestHeaders.Remove(FakeAuthenticationHandler.UserRolesHeader);
        return client;
    }
}

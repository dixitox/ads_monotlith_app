using System.Net;
using Xunit;

namespace RetailDecomposed.Tests;

/// <summary>
/// Tests for authentication and authorization in the decomposed application.
/// Tests anonymous access, authenticated access, and role-based access.
/// </summary>
public class AuthenticationTests : IClassFixture<DecomposedWebApplicationFactory>
{
    private readonly DecomposedWebApplicationFactory _factory;

    public AuthenticationTests(DecomposedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousUser_CanAccessHomePage()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousUser_CannotAccessProductsPage()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Products");

        // Assert - Should return Unauthorized (401) for API-style authentication
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousUser_CannotAccessCartPage()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Cart");

        // Assert - Should return Unauthorized (401) for API-style authentication
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousUser_CannotAccessCheckoutPage()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Checkout");

        // Assert - Should return Unauthorized (401) for API-style authentication
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousUser_CannotAccessOrdersPage()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Orders");

        // Assert - Should return Unauthorized (401) for API-style authentication
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedCustomer_CanAccessProductsPage()
    {
        // Arrange
        var client = _factory.CreateClient().AuthenticateAsCustomer();

        // Act
        var response = await client.GetAsync("/Products");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedCustomer_CanAccessCartPage()
    {
        // Arrange
        var client = _factory.CreateClient().AuthenticateAsCustomer();

        // Act
        var response = await client.GetAsync("/Cart");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedCustomer_CanAccessCheckoutPage()
    {
        // Arrange
        var client = _factory.CreateClient().AuthenticateAsCustomer();

        // Act
        var response = await client.GetAsync("/Checkout");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedCustomer_CanAccessOrdersPage()
    {
        // Arrange
        var client = _factory.CreateClient().AuthenticateAsCustomer();

        // Act
        var response = await client.GetAsync("/Orders");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedAdmin_CanAccessAllPages()
    {
        // Arrange
        var client = _factory.CreateClient().AuthenticateAsAdmin();

        // Act & Assert - Products
        var productsResponse = await client.GetAsync("/Products");
        productsResponse.EnsureSuccessStatusCode();

        // Act & Assert - Cart
        var cartResponse = await client.GetAsync("/Cart");
        cartResponse.EnsureSuccessStatusCode();

        // Act & Assert - Checkout
        var checkoutResponse = await client.GetAsync("/Checkout");
        checkoutResponse.EnsureSuccessStatusCode();

        // Act & Assert - Orders
        var ordersResponse = await client.GetAsync("/Orders");
        ordersResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DifferentUsers_HaveSeparateCarts()
    {
        // Arrange
        var client1 = _factory.CreateClient().AuthenticateAs("user1", "user1@test.com", "user1@test.com");
        var client2 = _factory.CreateClient().AuthenticateAs("user2", "user2@test.com", "user2@test.com");

        // Act - Add items to different carts
        await client1.PostAsync("/api/cart/user1@test.com/items?productId=1&quantity=2", null);
        await client2.PostAsync("/api/cart/user2@test.com/items?productId=2&quantity=3", null);

        // Get cart pages
        var cart1Response = await client1.GetAsync("/Cart");
        var cart2Response = await client2.GetAsync("/Cart");

        var cart1Content = await cart1Response.Content.ReadAsStringAsync();
        var cart2Content = await cart2Response.Content.ReadAsStringAsync();

        // Assert - Each user sees their own cart
        Assert.Contains("Test Product 1", cart1Content);
        Assert.DoesNotContain("Test Product 2", cart1Content);

        Assert.Contains("Test Product 2", cart2Content);
        Assert.DoesNotContain("Test Product 1", cart2Content);
    }

    [Fact]
    public async Task CustomUser_WithCustomRoles_CanBeAuthenticated()
    {
        // Arrange
        var client = _factory.CreateClient()
            .AuthenticateAs("custom-user", "custom@test.com", "custom@test.com", "Customer", "PowerUser");

        // Act
        var response = await client.GetAsync("/Products");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

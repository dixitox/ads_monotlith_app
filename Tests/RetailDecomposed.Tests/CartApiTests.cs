using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RetailDecomposed.Tests;

/// <summary>
/// Functional tests for the Cart API endpoints in the decomposed application.
/// Tests both API endpoints and ensures circular reference handling works.
/// </summary>
public class CartApiTests : IClassFixture<DecomposedWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CartApiTests(DecomposedWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCart_ForNewCustomer_Returns_EmptyCart()
    {
        // Arrange
        var client = _client.AuthenticateAs("testcustomer", "testcustomer", "testcustomer");
        
        // Act
        var response = await client.GetAsync("/api/cart/testcustomer");

        // Assert
        response.EnsureSuccessStatusCode();
        var cart = await response.Content.ReadFromJsonAsync<CartDto>();
        Assert.NotNull(cart);
        Assert.Equal("testcustomer", cart.CustomerId);
        Assert.Empty(cart.Lines);
    }

    [Fact]
    public async Task AddToCart_AddsItemSuccessfully()
    {
        // Arrange
        var customerId = "testcustomer2";
        var productId = 1;
        var quantity = 2;
        var client = _client.AuthenticateAs(customerId, customerId, customerId);

        // Act
        var response = await client.PostAsync(
            $"/api/cart/{customerId}/items?productId={productId}&quantity={quantity}", 
            null);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetCart_AfterAddingItem_Returns_CartWithItem()
    {
        // Arrange
        var customerId = "testcustomer3";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=2", null);

        // Act
        var response = await client.GetAsync($"/api/cart/{customerId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var cart = await response.Content.ReadFromJsonAsync<CartDto>();
        Assert.NotNull(cart);
        Assert.Single(cart.Lines);
        Assert.Equal("TEST-001", cart.Lines[0].Sku);
        Assert.Equal(2, cart.Lines[0].Quantity);
    }

    [Fact]
    public async Task GetCart_WithMultipleItems_Returns_AllItems()
    {
        // Arrange
        var customerId = "testcustomer4";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=1", null);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=2&quantity=3", null);

        // Act
        var response = await client.GetAsync($"/api/cart/{customerId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var cart = await response.Content.ReadFromJsonAsync<CartDto>();
        Assert.NotNull(cart);
        Assert.Equal(2, cart.Lines.Count);
    }

    [Fact]
    public async Task GetCart_DoesNotContainCircularReferences()
    {
        // Arrange - Add an item to cart
        var customerId = "testcustomer5";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=1", null);

        // Act - Get cart should not throw JsonException
        var response = await client.GetAsync($"/api/cart/{customerId}");

        // Assert - Successful deserialization means no circular reference issue
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
        
        // Verify we can deserialize successfully
        var cart = await response.Content.ReadFromJsonAsync<CartDto>();
        Assert.NotNull(cart);
    }

    [Fact]
    public async Task CartPage_Returns_Success()
    {
        // Arrange - Authenticate as customer
        var client = _client.AuthenticateAsCustomer();

        // Act
        var response = await client.GetAsync("/Cart");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCart_WithoutAuthentication_Returns_Unauthorized()
    {
        // Arrange - Anonymous client
        var client = _client;

        // Act
        var response = await client.GetAsync("/api/cart/testcustomer");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddToCart_WithoutAuthentication_Returns_Unauthorized()
    {
        // Arrange - Anonymous client
        var client = _client;

        // Act
        var response = await client.PostAsync("/api/cart/testcustomer/items?productId=1&quantity=1", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCart_WithMismatchedUserId_Returns_Forbidden()
    {
        // Arrange - Authenticate as one user, try to access another user's cart
        var client = _client.AuthenticateAs("user1", "user1", "user1");

        // Act
        var response = await client.GetAsync("/api/cart/user2");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddToCart_WithMismatchedUserId_Returns_Forbidden()
    {
        // Arrange - Authenticate as one user, try to add to another user's cart
        var client = _client.AuthenticateAs("user1", "user1", "user1");

        // Act
        var response = await client.PostAsync("/api/cart/user2/items?productId=1&quantity=1", null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RemoveFromCart_RemovesItemSuccessfully()
    {
        // Arrange - Add item first
        var customerId = "testcustomer_remove1";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=2", null);
        
        // Get the SKU
        var cartResponse = await client.GetAsync($"/api/cart/{customerId}");
        var cart = await cartResponse.Content.ReadFromJsonAsync<CartDto>();
        var sku = cart!.Lines[0].Sku;

        // Act - Remove the item
        var response = await client.DeleteAsync($"/api/cart/{customerId}/items/{sku}");

        // Assert
        response.EnsureSuccessStatusCode();
        
        // Verify item was removed
        var updatedCartResponse = await client.GetAsync($"/api/cart/{customerId}");
        var updatedCart = await updatedCartResponse.Content.ReadFromJsonAsync<CartDto>();
        Assert.Empty(updatedCart!.Lines);
    }

    [Fact]
    public async Task RemoveFromCart_WithMultipleItems_RemovesOnlySpecifiedItem()
    {
        // Arrange - Add multiple items
        var customerId = "testcustomer_remove2";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=1", null);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=2&quantity=1", null);
        
        // Get the first item's SKU
        var cartResponse = await client.GetAsync($"/api/cart/{customerId}");
        var cart = await cartResponse.Content.ReadFromJsonAsync<CartDto>();
        var skuToRemove = cart!.Lines[0].Sku;

        // Act - Remove first item
        var response = await client.DeleteAsync($"/api/cart/{customerId}/items/{skuToRemove}");

        // Assert
        response.EnsureSuccessStatusCode();
        
        // Verify only one item remains
        var updatedCartResponse = await client.GetAsync($"/api/cart/{customerId}");
        var updatedCart = await updatedCartResponse.Content.ReadFromJsonAsync<CartDto>();
        Assert.Single(updatedCart!.Lines);
        Assert.NotEqual(skuToRemove, updatedCart.Lines[0].Sku);
    }

    [Fact]
    public async Task RemoveFromCart_NonExistentItem_ReturnsSuccess()
    {
        // Arrange
        var customerId = "testcustomer_remove3";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);
        var nonExistentSku = "NONEXISTENT-SKU";

        // Act - Try to remove item that doesn't exist
        var response = await client.DeleteAsync($"/api/cart/{customerId}/items/{nonExistentSku}");

        // Assert - Should succeed (idempotent operation)
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task RemoveFromCart_WithoutAuthentication_Returns_Unauthorized()
    {
        // Arrange - Anonymous client
        var client = _client;

        // Act
        var response = await client.DeleteAsync("/api/cart/testcustomer/items/TEST-001");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RemoveFromCart_WithMismatchedUserId_Returns_Forbidden()
    {
        // Arrange - Authenticate as one user, try to remove from another user's cart
        var client = _client.AuthenticateAs("user1", "user1", "user1");

        // Act
        var response = await client.DeleteAsync("/api/cart/user2/items/TEST-001");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ClearCart_RemovesAllItemsSuccessfully()
    {
        // Arrange - Add multiple items
        var customerId = "testcustomer_clear1";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=2", null);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=2&quantity=3", null);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=3&quantity=1", null);
        
        // Verify items were added
        var cartResponse = await client.GetAsync($"/api/cart/{customerId}");
        var cart = await cartResponse.Content.ReadFromJsonAsync<CartDto>();
        Assert.Equal(3, cart!.Lines.Count);

        // Act - Clear cart
        var response = await client.DeleteAsync($"/api/cart/{customerId}");

        // Assert
        response.EnsureSuccessStatusCode();
        
        // Verify cart is empty
        var updatedCartResponse = await client.GetAsync($"/api/cart/{customerId}");
        var updatedCart = await updatedCartResponse.Content.ReadFromJsonAsync<CartDto>();
        Assert.Empty(updatedCart!.Lines);
    }

    [Fact]
    public async Task ClearCart_OnEmptyCart_ReturnsSuccess()
    {
        // Arrange - New customer with empty cart
        var customerId = "testcustomer_clear2";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);

        // Act - Clear already empty cart
        var response = await client.DeleteAsync($"/api/cart/{customerId}");

        // Assert - Should succeed (idempotent operation)
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ClearCart_WithoutAuthentication_Returns_Unauthorized()
    {
        // Arrange - Anonymous client
        var client = _client;

        // Act
        var response = await client.DeleteAsync("/api/cart/testcustomer");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ClearCart_WithMismatchedUserId_Returns_Forbidden()
    {
        // Arrange - Authenticate as one user, try to clear another user's cart
        var client = _client.AuthenticateAs("user1", "user1", "user1");

        // Act
        var response = await client.DeleteAsync("/api/cart/user2");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CartPage_WithItems_DisplaysRemoveButtons()
    {
        // Arrange - Add items and authenticate
        var customerId = "testcustomer_ui1";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=1", null);

        // Act - Get cart page
        var response = await client.GetAsync("/Cart");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        
        // Verify remove button exists
        Assert.Contains("remove-item-btn", content);
        Assert.Contains("bi-trash", content);
        Assert.Contains("Remove", content);
    }

    [Fact]
    public async Task CartPage_WithItems_DisplaysClearCartButton()
    {
        // Arrange - Add items and authenticate
        var customerId = "testcustomer_ui2";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);
        await client.PostAsync($"/api/cart/{customerId}/items?productId=1&quantity=1", null);

        // Act - Get cart page
        var response = await client.GetAsync("/Cart");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        
        // Verify clear cart button exists
        Assert.Contains("clear-cart-btn", content);
        Assert.Contains("Clear Cart", content);
        Assert.Contains("bi-trash3", content);
    }

    [Fact]
    public async Task CartPage_EmptyCart_DoesNotDisplayClearCartButton()
    {
        // Arrange - Authenticate with empty cart
        var customerId = "testcustomer_ui3";
        var client = _client.AuthenticateAs(customerId, customerId, customerId);

        // Act - Get cart page
        var response = await client.GetAsync("/Cart");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        
        // Verify cart is empty (the @if check)
        Assert.Contains("Your cart is empty", content);
        // The clear-cart-btn is inside the else block, so it shouldn't be rendered at all
        // Check that the button with id="clear-cart-btn" is not in the rendered HTML
        Assert.DoesNotContain("id=\"clear-cart-btn\"", content);
    }

    // DTO classes for deserialization
    private class CartDto
    {
        public int Id { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public List<CartLineDto> Lines { get; set; } = new();
    }

    private class CartLineDto
    {
        public int Id { get; set; }
        public int CartId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
    }
}

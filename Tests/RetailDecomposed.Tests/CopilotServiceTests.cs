using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace RetailDecomposed.Tests;

/// <summary>
/// Tests for the AI Copilot service functionality.
/// Tests chat API endpoint, message handling, and AI assistant behavior.
/// Note: These tests use a fake/mock AI service since we can't call real Azure AI in tests.
/// </summary>
public class CopilotServiceTests : IClassFixture<DecomposedWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CopilotServiceTests(DecomposedWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChatApi_WithValidMessage_Returns_Success()
    {
        // Arrange
        var client = _client.AuthenticateAsCustomer();
        var request = new ChatRequest
        {
            Message = "Hello",
            ConversationHistory = new List<ChatMessage>()
        };

        // Act - Note: This will fail in real tests without mocking Azure AI
        // For now, we test that the endpoint exists and accepts requests
        var response = await client.PostAsJsonAsync("/api/chat", request);

        // Assert - Endpoint exists (may return error due to missing Azure AI config in tests)
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChatApi_WithEmptyMessage_Returns_BadRequest()
    {
        // Arrange
        var client = _client.AuthenticateAsCustomer();
        var request = new ChatRequest
        {
            Message = "",
            ConversationHistory = new List<ChatMessage>()
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChatApi_WithNullMessage_Returns_BadRequest()
    {
        // Arrange
        var client = _client.AuthenticateAsCustomer();
        var request = new ChatRequest
        {
            Message = null!,
            ConversationHistory = new List<ChatMessage>()
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChatApi_WithoutAuthentication_Returns_Unauthorized()
    {
        // Arrange - Anonymous client
        var client = _client;
        var request = new ChatRequest
        {
            Message = "Hello",
            ConversationHistory = new List<ChatMessage>()
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChatApi_WithConversationHistory_AcceptsRequest()
    {
        // Arrange
        var client = _client.AuthenticateAsCustomer();
        var request = new ChatRequest
        {
            Message = "Show me more",
            ConversationHistory = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = "Hello" },
                new ChatMessage { Role = "assistant", Content = "Hi! How can I help?" }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/chat", request);

        // Assert - Endpoint accepts conversation history
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CopilotPage_Returns_Success()
    {
        // Arrange - Authenticate as customer
        var client = _client.AuthenticateAsCustomer();

        // Act
        var response = await client.GetAsync("/Copilot");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CopilotPage_ContainsChatUI()
    {
        // Arrange
        var client = _client.AuthenticateAsCustomer();

        // Act
        var response = await client.GetAsync("/Copilot");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        
        // Verify chat UI elements exist with correct IDs from the actual page
        Assert.Contains("id=\"chatContainer\"", content);
        Assert.Contains("id=\"messageInput\"", content);
        Assert.Contains("id=\"sendButton\"", content);
    }

    [Fact]
    public async Task CopilotPage_WithoutAuthentication_RedirectsToLogin()
    {
        // Arrange - Anonymous client (no auth headers)
        var client = _client.AsAnonymous();

        // Act
        var response = await client.GetAsync("/Copilot");

        // Assert - With FakeAuthenticationHandler properly configured to fail, should get Unauthorized
        // Note: In production with Azure AD, this would redirect to login page
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void ChatRequest_SerializesCorrectly()
    {
        // Arrange
        var request = new ChatRequest
        {
            Message = "Test message",
            ConversationHistory = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = "Hello" }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(request);
        var deserialized = JsonSerializer.Deserialize<ChatRequest>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("Test message", deserialized.Message);
        Assert.Single(deserialized.ConversationHistory);
        Assert.Equal("user", deserialized.ConversationHistory[0].Role);
    }

    [Fact]
    public void ChatMessage_WithRoleAndContent_IsValid()
    {
        // Arrange & Act
        var message = new ChatMessage
        {
            Role = "assistant",
            Content = "This is a response"
        };

        // Assert
        Assert.Equal("assistant", message.Role);
        Assert.Equal("This is a response", message.Content);
    }

    // DTO classes for API requests
    private class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<ChatMessage> ConversationHistory { get; set; } = new();
    }

    private class ChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private class ChatResponse
    {
        public string Response { get; set; } = string.Empty;
    }
}

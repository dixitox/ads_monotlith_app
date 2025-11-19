using RetailMonolith.Models;

namespace RetailMonolith.Services
{
    public interface IChatService
    {
        Task<string> GetChatResponseAsync(string userMessage, string conversationHistory, CancellationToken ct = default);
        Task<string> BuildSystemPromptAsync(CancellationToken ct = default);
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<ChatMessage> History { get; set; } = new();
    }

    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty; // "user" or "assistant"
        public string Content { get; set; } = string.Empty;
    }

    public class ChatResponse
    {
        public string Message { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Error { get; set; }
    }
}

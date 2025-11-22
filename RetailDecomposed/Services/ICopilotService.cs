namespace RetailDecomposed.Services
{
    public interface ICopilotService
    {
        /// <summary>
        /// Gets a chat response from the AI copilot with product recommendations.
        /// </summary>
        /// <param name="userMessage">The user's message or question</param>
        /// <param name="conversationHistory">Optional conversation history for context</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The AI's response</returns>
        Task<string> GetChatResponseAsync(
            string userMessage, 
            List<ChatMessage>? conversationHistory = null, 
            CancellationToken ct = default);
    }

    /// <summary>
    /// Represents a chat message in the conversation.
    /// </summary>
    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty; // "user" or "assistant"
        public string Content { get; set; } = string.Empty;
    }
}

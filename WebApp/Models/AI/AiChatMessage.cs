namespace WebApp.Models.AI
{
    /// <summary>
    /// Representerar ett meddelande i en AI-konversation
    /// </summary>
    public sealed class AiChatMessage
    {
        /// <summary>
        /// "system", "user" eller "assistant"
        /// </summary>
        public string Role { get; set; } = "user";

        public string Content { get; set; } = string.Empty;

        // Factory helpers
        public static AiChatMessage System(string content) =>
            new() { Role = "system", Content = content };

        public static AiChatMessage User(string content) =>
            new() { Role = "user", Content = content };

        public static AiChatMessage Assistant(string content) =>
            new() { Role = "assistant", Content = content };

        public static AiChatMessage Admin(string content) =>
            new() { Role = "system", Content = content }; // admin behandlas som system
    }
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WebApp.Services.Application
{
    public interface IOpenAiChatService
    {
        Task<OpenAiChatResult> AskAsync(
            string userMessage,
            IReadOnlyList<OpenAiChatMessage>? history = null,
            CancellationToken ct = default);
    }

    public sealed class OpenAiChatMessage
    {
        public string Role { get; set; } = "user"; // system|user|assistant
        public string Content { get; set; } = string.Empty;
    }

    public sealed class OpenAiChatResult
    {
        public string Answer { get; set; } = string.Empty;
        public string? RawJson { get; set; }
        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }
        public int? TotalTokens { get; set; }
        public string? ModelDeployment { get; set; }
        public int RetryCount { get; set; }
        public long DurationMs { get; set; }
    }
}

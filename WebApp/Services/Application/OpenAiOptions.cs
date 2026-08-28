// Defines Azure OpenAI configuration supplied by the deployment environment.
namespace WebApp.Services.Application
{
    public sealed class OpenAiOptions
    {
        // Azure OpenAI resource endpoint.
        public string Endpoint { get; set; } = string.Empty;

        // Azure OpenAI key (NOT OpenAI platform key)
        public string ApiKey { get; set; } = string.Empty;

        // Azure OpenAI deployment name, ex: "gpt-4.1"
        public string Deployment { get; set; } = string.Empty;

        // Azure OpenAI api-version, ex: "2024-10-21" (exempel)
        public string ApiVersion { get; set; } = "2024-10-21";

        // Number of retries for temporary throttling and server failures.
        public int MaxRetryCount { get; set; } = 2;
    }
}

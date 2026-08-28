// Verifies structured JSON requests and bounded retries for temporary model failures.
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using WebApp.Services.Application;

namespace WebApp.Tests;

public sealed class OpenAiChatServiceProductionTests
{
    [Fact]
    public async Task AskAsync_RetriesThrottleAndRequestsJsonForStructuredPrompt()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("""{"error":{"message":"busy"}}""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"content":"{\"sql\":\"SELECT 1\"}"}}],"usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}""",
                    Encoding.UTF8,
                    "application/json")
            });
        var service = new OpenAiChatService(
            new TestHttpClientFactory(new HttpClient(handler)),
            Options.Create(new OpenAiOptions
            {
                ApiKey = "test-key",
                Endpoint = "https://example.openai.azure.com/",
                Deployment = "test-deployment",
                ApiVersion = "2024-10-21",
                MaxRetryCount = 2
            }));

        var result = await service.AskAsync(
            "Generate SQL",
            [new OpenAiChatMessage { Role = "system", Content = "Output ONLY valid JSON." }]);

        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.Contains("\"response_format\":{\"type\":\"json_object\"}", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.Contains("\"temperature\":0", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.Equal(1, result.RetryCount);
        Assert.Equal("test-deployment", result.ModelDeployment);
        Assert.Equal(15, result.TotalTokens);
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public TestHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public List<string> RequestBodies { get; } = [];

        public SequenceHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }
}

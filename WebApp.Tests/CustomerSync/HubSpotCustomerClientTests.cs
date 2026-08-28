using System.Net;
using System.Net.Http;
using System.Text;
using WebApp.Services.Integration.CustomerSync.HubSpot;

namespace WebApp.Tests.CustomerSync;

public sealed class HubSpotCustomerClientTests
{
    [Fact]
    public async Task ListCompaniesAsync_Reads_All_Pages_Until_HubSpot_Returns_No_Next_Cursor()
    {
        var handler = new SequencedResponseHandler(
            """
            {"results":[{"id":"1","properties":{"name":"First"}},{"id":"2","properties":{"name":"Second"}}],"paging":{"next":{"after":"cursor-2"}}}
            """,
            """
            {"results":[{"id":"3","properties":{"name":"Third"}}]}
            """);

        var client = new HubSpotCustomerClient(new StubHttpClientFactory(handler));

        var companies = await client.ListCompaniesAsync(
            new CustomerSyncHubSpotConnection
            {
                Token = "token"
            },
            limit: 25,
            CancellationToken.None);

        Assert.Equal(3, companies.Count);
        Assert.Equal(2, handler.RequestCount);
        Assert.Contains(handler.RequestedUris, uri => uri.Query.Contains("limit=25", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(handler.RequestedUris, uri => uri.Query.Contains("after=cursor-2", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            _client = new HttpClient(handler);
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class SequencedResponseHandler : HttpMessageHandler
    {
        private readonly IReadOnlyList<string> _responses;
        private int _index;

        public SequencedResponseHandler(params string[] responses)
        {
            _responses = responses;
        }

        public List<Uri> RequestedUris { get; } = new();

        public int RequestCount => RequestedUris.Count;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUris.Add(request.RequestUri ?? throw new InvalidOperationException("Missing request uri."));

            if (_index >= _responses.Count)
                throw new InvalidOperationException("Unexpected extra HubSpot request.");

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responses[_index++], Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}

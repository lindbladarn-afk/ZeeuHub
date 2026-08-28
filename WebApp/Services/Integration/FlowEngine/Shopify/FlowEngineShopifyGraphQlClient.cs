using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineShopifyGraphQlClient : IFlowEngineShopifyGraphQlClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public FlowEngineShopifyGraphQlClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<T> PostAsync<T>(
        Uri endpointUrl,
        string accessToken,
        string query,
        Dictionary<string, object?> variables,
        string operationName,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var client = _httpClientFactory.CreateClient("Integration.Shopify");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
        request.Headers.Add("X-Shopify-Access-Token", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(
            JsonSerializer.Serialize(new GraphQlRequest(query, operationName, variables), JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Shopify GraphQL svarade med {(int)response.StatusCode}: {TrimForError(body)}");

        var parsed = JsonSerializer.Deserialize<GraphQlEnvelope<T>>(body, JsonOptions);
        var errors = parsed?.Errors?
                         .Select(error => error.Message)
                         .Where(message => !string.IsNullOrWhiteSpace(message))
                         .Cast<string>()
                         .ToList()
                     ?? new List<string>();
        if (errors.Count > 0 && parsed?.Data is null)
            throw new InvalidOperationException(string.Join(" | ", errors));

        if (parsed?.Data is null)
            throw new InvalidOperationException("Shopify GraphQL returnerade inget data payload.");

        return parsed.Data;
    }

    private static string TrimForError(string value)
    {
        var normalized = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length <= 300 ? normalized : normalized[..300];
    }

    private sealed record GraphQlRequest(string Query, string OperationName, Dictionary<string, object?> Variables);

    private sealed class GraphQlEnvelope<TData>
    {
        public TData? Data { get; set; }
        public List<GraphQlError>? Errors { get; set; }
    }

    private sealed class GraphQlError
    {
        public string Message { get; set; } = string.Empty;
    }
}

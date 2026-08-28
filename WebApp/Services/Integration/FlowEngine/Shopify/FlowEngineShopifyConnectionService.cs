using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineShopifyConnectionService : IFlowEngineShopifyConnectionService
{
    private const string DefaultShopifyApiVersion = "2025-01";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<IntegrationOptions> _integrationOptions;

    public FlowEngineShopifyConnectionService(
        IHttpClientFactory httpClientFactory,
        IOptions<IntegrationOptions> integrationOptions)
    {
        _httpClientFactory = httpClientFactory;
        _integrationOptions = integrationOptions;
    }

    public async Task<FlowEngineShopifyConnectionContext> CreateAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var config = ResolveConfig(companyId);
        var storeDomain = ResolveStoreDomain(config.BaseUrl!);
        var endpointUrl = BuildShopifyGraphQlEndpoint(storeDomain);
        var accessToken = await GetShopifyAccessTokenAsync(config, storeDomain, cancellationToken);

        return new FlowEngineShopifyConnectionContext(config, storeDomain, endpointUrl, accessToken);
    }

    private async Task<string> GetShopifyAccessTokenAsync(
        IntegrationSourceConfig shopifyConfig,
        string storeDomain,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(shopifyConfig.Token))
            return shopifyConfig.Token.Trim();

        if (string.IsNullOrWhiteSpace(shopifyConfig.AppId) || string.IsNullOrWhiteSpace(shopifyConfig.AppSecret))
            throw new InvalidOperationException("Shopify integration maste ha Token eller AppId/AppSecret for FlowEngine.");

        var client = _httpClientFactory.CreateClient("Integration.Shopify");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = shopifyConfig.AppId.Trim(),
            ["client_secret"] = shopifyConfig.AppSecret.Trim(),
            ["grant_type"] = "client_credentials"
        });
        using var response = await client.PostAsync(BuildShopifyAccessTokenUrl(storeDomain), content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Shopify auth svarade med {(int)response.StatusCode}: {TrimForError(body)}");

        var parsed = JsonSerializer.Deserialize<ShopifyAccessTokenResponse>(body, JsonOptions);
        var token = parsed?.AccessToken?.Trim();
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Shopify auth returnerade ingen access token.");

        return token;
    }

    private IntegrationSourceConfig ResolveConfig(Guid companyId)
    {
        var company = _integrationOptions.Value.Companies.FirstOrDefault(entry => entry.CompanyId == companyId);
        var config = company?.GetSource(IntegrationSource.Shopify);
        if (config is null || string.IsNullOrWhiteSpace(config.BaseUrl))
            throw new InvalidOperationException("Shopify integration saknar BaseUrl for FlowEngine.");

        if (string.IsNullOrWhiteSpace(config.Token) &&
            (string.IsNullOrWhiteSpace(config.AppId) || string.IsNullOrWhiteSpace(config.AppSecret)))
        {
            throw new InvalidOperationException("Shopify integration maste ha Token eller AppId/AppSecret for FlowEngine.");
        }

        return config;
    }

    private static string ResolveStoreDomain(string rawBaseUrl)
    {
        var trimmed = rawBaseUrl.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri) && !string.IsNullOrWhiteSpace(absoluteUri.Host))
            return absoluteUri.Host;

        trimmed = trimmed.Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim('/');

        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException("Shopify BaseUrl maste innehalla store domain.");

        return trimmed;
    }

    private static Uri BuildShopifyGraphQlEndpoint(string storeDomain)
        => new($"https://{storeDomain}/admin/api/{DefaultShopifyApiVersion}/graphql.json");

    private static Uri BuildShopifyAccessTokenUrl(string storeDomain)
        => new($"https://{storeDomain}/admin/oauth/access_token");

    private static string TrimForError(string value)
    {
        var normalized = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length <= 300 ? normalized : normalized[..300];
    }

    private sealed class ShopifyAccessTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }
    }
}

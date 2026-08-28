using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Entities.Application;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.Services.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineHealthProbeService : IFlowEngineHealthProbeService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ProbeCacheDuration = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<IntegrationOptions> _integrationOptions;
    private readonly IOptions<AkeneoOptions> _akeneoOptions;
    private readonly IJeevesAuthService _jeevesAuthService;
    private readonly IFlowEngineCentraConnectionService _centraConnectionService;
    private readonly IMemoryCache _cache;

    public FlowEngineHealthProbeService(
        IHttpClientFactory httpClientFactory,
        IOptions<IntegrationOptions> integrationOptions,
        IOptions<AkeneoOptions> akeneoOptions,
        IJeevesAuthService jeevesAuthService,
        IFlowEngineCentraConnectionService centraConnectionService,
        IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _integrationOptions = integrationOptions;
        _akeneoOptions = akeneoOptions;
        _jeevesAuthService = jeevesAuthService;
        _centraConnectionService = centraConnectionService;
        _cache = cache;
    }

    public async Task<IReadOnlyList<FlowEngineSystemStatusViewModel>> ProbeAsync(
        UserSession? sessionUser,
        string activeSection,
        JeevesRuntimeContext? runtimeContext,
        bool testMode,
        CancellationToken cancellationToken = default)
    {
        var normalizedSection = FlowEngineSectionKeys.Normalize(activeSection);
        var resolvedCompanyId = runtimeContext?.CompanyId
            ?? sessionUser?.CompanyId;

        var companyConfig = resolvedCompanyId is Guid companyId
            ? _integrationOptions.Value.Companies.FirstOrDefault(entry => entry.CompanyId == companyId)
            : null;

        var probes = normalizedSection switch
        {
            FlowEngineSectionKeys.Centra => new[] { ProbeCentraAsync(resolvedCompanyId, companyConfig, testMode, cancellationToken) },
            FlowEngineSectionKeys.Shopify => new[] { ProbeShopifyAsync(companyConfig, cancellationToken) },
            FlowEngineSectionKeys.Jeeves => new[] { ProbeJeevesAsync(companyConfig, runtimeContext, cancellationToken) },
            FlowEngineSectionKeys.Akeneo => new[] { ProbeAkeneoAsync(cancellationToken) },
            _ => new[]
            {
                ProbeCentraAsync(resolvedCompanyId, companyConfig, testMode, cancellationToken),
                ProbeShopifyAsync(companyConfig, cancellationToken),
                ProbeJeevesAsync(companyConfig, runtimeContext, cancellationToken),
                ProbeAkeneoAsync(cancellationToken)
            }
        };

        return await Task.WhenAll(probes);
    }

    private async Task<FlowEngineSystemStatusViewModel> ProbeCentraAsync(
        Guid? companyId,
        IntegrationCompanyConfig? companyConfig,
        bool testMode,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"flowengine:probe:centra:{companyId}:{testMode}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ProbeCacheDuration;

            if (!companyId.HasValue || companyConfig?.GetSource(IntegrationSource.Centra) is null)
                return BuildStatus("centra", "Centra", false, "Saknas");

            try
            {
                var source = _centraConnectionService.ResolveConfig(companyId.Value, "health", testMode);
                var client = _centraConnectionService.CreateClient(source);
                var requestUri = _centraConnectionService.GetRequestUri(source);

                using var probeToken = CreateProbeToken(cancellationToken);
                using var response = await client.PostAsJsonAsync(requestUri, new { query = "{ __typename }" }, probeToken.Token);
                return BuildReachabilityStatus("centra", "Centra", response.IsSuccessStatusCode, (int)response.StatusCode);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("BaseUrl", StringComparison.OrdinalIgnoreCase))
            {
                return BuildStatus("centra", "Centra", false, "Saknas");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("token", StringComparison.OrdinalIgnoreCase))
            {
                return BuildStatus("centra", "Centra", false, "Ej konfigurerad");
            }
            catch
            {
                return BuildStatus("centra", "Centra", false, "Fel");
            }
        }) ?? BuildStatus("centra", "Centra", false, "Fel");
    }

    private async Task<FlowEngineSystemStatusViewModel> ProbeShopifyAsync(IntegrationCompanyConfig? companyConfig, CancellationToken cancellationToken)
    {
        var companyId = companyConfig?.CompanyId;
        var cacheKey = $"flowengine:probe:shopify:{companyId}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ProbeCacheDuration;

            var source = companyConfig?.GetSource(IntegrationSource.Shopify);
            if (source is null || string.IsNullOrWhiteSpace(source.BaseUrl))
                return BuildStatus("shopify", "Shopify", false, "Saknas");

            try
            {
                var storeDomain = ResolveStoreDomain(source.BaseUrl);
                var token = await GetShopifyAccessTokenAsync(source, storeDomain, cancellationToken);
                var endpointUrl = new Uri($"https://{storeDomain}/admin/api/2025-01/graphql.json");

                var client = _httpClientFactory.CreateClient("Integration.Shopify");
                using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
                request.Headers.Add("X-Shopify-Access-Token", token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Content = new StringContent(
                    JsonSerializer.Serialize(new { query = "query ProbeShop { shop { name } }" }, JsonOptions),
                    Encoding.UTF8,
                    "application/json");

                using var probeToken = CreateProbeToken(cancellationToken);
                using var response = await client.SendAsync(request, probeToken.Token);
                return BuildReachabilityStatus("shopify", "Shopify", response.IsSuccessStatusCode, (int)response.StatusCode);
            }
            catch (InvalidOperationException)
            {
                return BuildStatus("shopify", "Shopify", false, "Ej konfigurerad");
            }
            catch
            {
                return BuildStatus("shopify", "Shopify", false, "Fel");
            }
        }) ?? BuildStatus("shopify", "Shopify", false, "Fel");
    }

    private async Task<FlowEngineSystemStatusViewModel> ProbeJeevesAsync(
        IntegrationCompanyConfig? companyConfig,
        JeevesRuntimeContext? runtimeContext,
        CancellationToken cancellationToken)
    {
        var companyId = runtimeContext?.CompanyId;
        var cacheKey = $"flowengine:probe:jeeves:{companyId}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ProbeCacheDuration;

            if (runtimeContext is null)
                return BuildStatus("jeeves", "Jeeves", false, "Runtime saknas");

            var source = companyConfig?.GetSource(IntegrationSource.Jeeves);
            if (source is null || string.IsNullOrWhiteSpace(source.BaseUrl) || string.IsNullOrWhiteSpace(source.AuthUrl))
                return BuildStatus("jeeves", "Jeeves", false, "Saknas");

            if (!companyConfig?.JeevesCompanyCode.HasValue ?? true)
                return BuildStatus("jeeves", "Jeeves", false, "Ej konfigurerad");

            try
            {
                var token = await _jeevesAuthService.GetAccessTokenAsync(
                    $"{runtimeContext.CompanyId}:jeeves",
                    source.AuthUrl,
                    source.AppId ?? string.Empty,
                    source.AppSecret ?? string.Empty,
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(token))
                    return BuildStatus("jeeves", "Jeeves", false, "Auth misslyckades");

                var client = _httpClientFactory.CreateClient("Integration.Jeeves");
                client.BaseAddress = new Uri(source.BaseUrl.TrimEnd('/') + "/");

                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"orders?c_foretagkod={companyConfig!.JeevesCompanyCode!.Value}&c_pagesize=1&c_pagenumber=1");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var probeToken = CreateProbeToken(cancellationToken);
                using var response = await client.SendAsync(request, probeToken.Token);
                var reachable = (int)response.StatusCode < 500;
                return BuildReachabilityStatus("jeeves", "Jeeves", reachable, (int)response.StatusCode);
            }
            catch
            {
                return BuildStatus("jeeves", "Jeeves", false, "Fel");
            }
        }) ?? BuildStatus("jeeves", "Jeeves", false, "Fel");
    }

    private async Task<FlowEngineSystemStatusViewModel> ProbeAkeneoAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "flowengine:probe:akeneo";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ProbeCacheDuration;

            var options = _akeneoOptions.Value;
            if (!options.Enabled)
                return BuildStatus("akeneo", "Akeneo", false, "Avstängd");

            if (string.IsNullOrWhiteSpace(options.BaseUrl))
                return BuildStatus("akeneo", "Akeneo", false, "Saknas");

            if (string.IsNullOrWhiteSpace(options.ClientId)
                || string.IsNullOrWhiteSpace(options.ClientSecret)
                || string.IsNullOrWhiteSpace(options.Username)
                || string.IsNullOrWhiteSpace(options.Password))
            {
                return BuildStatus("akeneo", "Akeneo", false, "Ej konfigurerad");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("Integration.Akeneo");
                var tokenUrl = $"{options.BaseUrl.TrimEnd('/')}/api/oauth/v1/token";
                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = options.ClientId,
                    ["client_secret"] = options.ClientSecret,
                    ["username"] = options.Username,
                    ["password"] = options.Password
                });

                using var probeToken = CreateProbeToken(cancellationToken);
                using var response = await client.PostAsync(tokenUrl, content, probeToken.Token);
                var reachable = (int)response.StatusCode < 500;
                return BuildReachabilityStatus("akeneo", "Akeneo", reachable, (int)response.StatusCode);
            }
            catch
            {
                return BuildStatus("akeneo", "Akeneo", false, "Fel");
            }
        }) ?? BuildStatus("akeneo", "Akeneo", false, "Fel");
    }

    private async Task<string> GetShopifyAccessTokenAsync(
        IntegrationSourceConfig shopifyConfig,
        string storeDomain,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(shopifyConfig.Token))
            return shopifyConfig.Token.Trim();

        if (string.IsNullOrWhiteSpace(shopifyConfig.AppId) || string.IsNullOrWhiteSpace(shopifyConfig.AppSecret))
            throw new InvalidOperationException("Shopify integration maste ha Token eller AppId/AppSecret.");

        var client = _httpClientFactory.CreateClient("Integration.Shopify");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = shopifyConfig.AppId.Trim(),
            ["client_secret"] = shopifyConfig.AppSecret.Trim(),
            ["grant_type"] = "client_credentials"
        });

        using var probeToken = CreateProbeToken(cancellationToken);
        using var response = await client.PostAsync(
            new Uri($"https://{storeDomain}/admin/oauth/access_token"),
            content,
            probeToken.Token);
        var body = await response.Content.ReadAsStringAsync(probeToken.Token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Shopify auth svarade med {(int)response.StatusCode}.");

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("access_token", out var accessToken))
            throw new InvalidOperationException("Shopify auth returnerade ingen access token.");

        var token = accessToken.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Shopify auth returnerade ingen access token.");

        return token;
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

    private static CancellationTokenSource CreateProbeToken(CancellationToken cancellationToken)
    {
        var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
    }

    private static FlowEngineSystemStatusViewModel BuildReachabilityStatus(string key, string label, bool reachable, int statusCode)
    {
        return reachable
            ? BuildStatus(key, label, true, "OK")
            : BuildStatus(key, label, false, $"Fel ({statusCode})");
    }

    private static FlowEngineSystemStatusViewModel BuildStatus(string key, string label, bool isReady, string statusText)
    {
        return new FlowEngineSystemStatusViewModel
        {
            Key = key,
            Label = label,
            IsReady = isReady,
            StatusText = statusText
        };
    }
}

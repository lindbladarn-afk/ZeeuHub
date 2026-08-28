using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using WebApp.Models.Integration;
using WebApp.Services.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraConnectionService : IFlowEngineCentraConnectionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<IntegrationOptions> _integrationOptions;

    public FlowEngineCentraConnectionService(
        IHttpClientFactory httpClientFactory,
        IOptions<IntegrationOptions> integrationOptions)
    {
        _httpClientFactory = httpClientFactory;
        _integrationOptions = integrationOptions;
    }

    public IntegrationSourceConfig ResolveConfig(Guid companyId, string flowLabel, bool testMode = false)
    {
        var company = _integrationOptions.Value.Companies.FirstOrDefault(entry => entry.CompanyId == companyId);
        var config = company?.GetSource(IntegrationSource.Centra);
        if (config is null)
            throw new InvalidOperationException($"Centra integration saknar BaseUrl for FlowEngine {flowLabel}.");

        var effectiveConfig = testMode
            ? new IntegrationSourceConfig
            {
                Source = config.Source,
                BaseUrl = string.IsNullOrWhiteSpace(config.TestBaseUrl) ? config.BaseUrl : config.TestBaseUrl,
                Token = string.IsNullOrWhiteSpace(config.TestToken) ? config.Token : config.TestToken,
                AuthUrl = string.IsNullOrWhiteSpace(config.TestAuthUrl) ? config.AuthUrl : config.TestAuthUrl,
                AppId = string.IsNullOrWhiteSpace(config.TestAppId) ? config.AppId : config.TestAppId,
                AppSecret = string.IsNullOrWhiteSpace(config.TestAppSecret) ? config.AppSecret : config.TestAppSecret,
                Username = string.IsNullOrWhiteSpace(config.TestUsername) ? config.Username : config.TestUsername,
                Password = string.IsNullOrWhiteSpace(config.TestPassword) ? config.Password : config.TestPassword,
                GoodsOwnerId = config.TestGoodsOwnerId ?? config.GoodsOwnerId,
                Enabled = config.Enabled,
                TestBaseUrl = config.TestBaseUrl,
                TestToken = config.TestToken,
                TestAuthUrl = config.TestAuthUrl,
                TestAppId = config.TestAppId,
                TestAppSecret = config.TestAppSecret,
                TestUsername = config.TestUsername,
                TestPassword = config.TestPassword,
                TestGoodsOwnerId = config.TestGoodsOwnerId
            }
            : config;

        if (string.IsNullOrWhiteSpace(effectiveConfig.BaseUrl))
            throw new InvalidOperationException($"Centra integration saknar BaseUrl for FlowEngine {flowLabel}.");

        if (string.IsNullOrWhiteSpace(effectiveConfig.Token))
            throw new InvalidOperationException($"Centra integration saknar token for FlowEngine {flowLabel}.");

        return effectiveConfig;
    }

    public HttpClient CreateClient(IntegrationSourceConfig centraConfig)
    {
        var client = _httpClientFactory.CreateClient("Integration.Centra");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", centraConfig.Token);
        return client;
    }

    public Uri GetRequestUri(IntegrationSourceConfig centraConfig)
    {
        return new Uri(centraConfig.BaseUrl!, UriKind.Absolute);
    }
}

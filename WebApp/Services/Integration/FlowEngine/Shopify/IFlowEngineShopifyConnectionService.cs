using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineShopifyConnectionService
{
    Task<FlowEngineShopifyConnectionContext> CreateAsync(Guid companyId, CancellationToken cancellationToken = default);
}

public sealed record FlowEngineShopifyConnectionContext(
    IntegrationSourceConfig Config,
    string StoreDomain,
    Uri EndpointUrl,
    string AccessToken);

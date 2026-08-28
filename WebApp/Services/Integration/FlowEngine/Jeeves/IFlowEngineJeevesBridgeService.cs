using WebApp.Models.Integration;
using WebApp.Services.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineJeevesBridgeService
{
    IntegrationSourceConfig ResolveConfig(Guid companyId, string operationLabel);
    Task<FlowEngineJeevesOrderCheckResult> CheckOrderAsync(Guid companyId, IntegrationSourceConfig config, string externalOrderNumber, CancellationToken cancellationToken = default);
    Task<bool> OrderExistsAsync(IntegrationSourceConfig config, Guid companyId, int jeevesCompanyCode, string externalOrderNumber, CancellationToken cancellationToken = default);
    Task CreateOrderAsync(IntegrationSourceConfig config, Guid companyId, FlowEngineShopifyJeevesOrderPayload payload, CancellationToken cancellationToken = default);
}

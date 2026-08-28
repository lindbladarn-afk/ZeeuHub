using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineCentraJeevesBridgeService
{
    IntegrationSourceConfig ResolveConfig(Guid companyId, string operationLabel);
    Task<FlowEngineJeevesOrderCheckResult> CheckOrderAsync(Guid companyId, IntegrationSourceConfig config, string externalOrderNumber, CancellationToken cancellationToken);
    Task<bool> OrderExistsAsync(Guid companyId, IntegrationSourceConfig config, int companyCode, string externalOrderId, CancellationToken cancellationToken);
    Task CreateOrderAsync(Guid companyId, IntegrationSourceConfig config, string jsonPayload, string operationLabel, CancellationToken cancellationToken);
}

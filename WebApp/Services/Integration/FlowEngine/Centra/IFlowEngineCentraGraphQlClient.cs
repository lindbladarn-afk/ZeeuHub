using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineCentraGraphQlClient
{
    Task<string> PostAsync(IntegrationSourceConfig centraConfig, object payload, CancellationToken cancellationToken = default);
}

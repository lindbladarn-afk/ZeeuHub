using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineCentraConnectionService
{
    IntegrationSourceConfig ResolveConfig(Guid companyId, string flowLabel, bool testMode = false);
    HttpClient CreateClient(IntegrationSourceConfig centraConfig);
    Uri GetRequestUri(IntegrationSourceConfig centraConfig);
}

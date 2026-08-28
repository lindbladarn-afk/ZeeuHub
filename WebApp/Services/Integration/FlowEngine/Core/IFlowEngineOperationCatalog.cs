using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineOperationCatalog
{
    IReadOnlyList<FlowEngineOperationDefinition> GetAll();
}

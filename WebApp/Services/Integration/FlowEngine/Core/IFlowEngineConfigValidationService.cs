using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineConfigValidationService
{
    Task<FlowEngineOperationExecutionData> ExecuteAsync(
        JeevesRuntimeContext runtimeContext,
        CancellationToken cancellationToken = default);
}

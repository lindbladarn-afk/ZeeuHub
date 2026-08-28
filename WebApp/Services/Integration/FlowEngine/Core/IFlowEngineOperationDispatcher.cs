using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineOperationDispatcher
{
    Task<FlowEngineOperationExecutionData> DispatchAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken = default);
}

using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineCentraCreateShipmentsService
{
    Task<FlowEngineOperationExecutionData> ExecuteAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken = default);
}

using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineCentraSendReturnsResultFactory
{
    FlowEngineOperationExecutionData CreateBulkResult(
        JeevesRuntimeContext runtimeContext,
        string date,
        int? limit,
        bool dryRun,
        FlowEngineSendReturnsCounts counts,
        double runtimeSeconds,
        IReadOnlyList<FlowEngineSendReturnsRow> nonCleanRows);

    FlowEngineOperationExecutionData CreateSingleResult(
        JeevesRuntimeContext runtimeContext,
        int returnId,
        bool dryRun,
        FlowEngineSendReturnsRow result);
}

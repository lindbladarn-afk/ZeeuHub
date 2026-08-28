using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineCentraSendOrdersResultFactory
{
    FlowEngineOperationExecutionData CreateBulkResult(
        JeevesRuntimeContext runtimeContext,
        string date,
        int? limit,
        bool dryRun,
        bool skipJeevesCheck,
        FlowEngineSendOrdersCounts counts,
        double runtimeSeconds,
        IReadOnlyList<FlowEngineSendOrdersRow> nonCleanRows);

    FlowEngineOperationExecutionData CreateSingleResult(
        JeevesRuntimeContext runtimeContext,
        string orderId,
        bool dryRun,
        bool skipJeevesCheck,
        FlowEngineSendOrdersRow result);
}

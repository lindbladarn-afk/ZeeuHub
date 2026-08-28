using System.Text.Json;
using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineCentraReadResultFactory
{
    FlowEngineOperationExecutionData CreateFetchOrderResult(JeevesRuntimeContext runtimeContext, string orderId, string body);
    FlowEngineOperationExecutionData CreateFetchReturnResult(JeevesRuntimeContext runtimeContext, int returnId, string body);
    FlowEngineOperationExecutionData CreateFetchOrdersResult(
        JeevesRuntimeContext runtimeContext,
        string selectionKind,
        IReadOnlyList<DateTime> dates,
        DateTime sinceUtc,
        DateTime untilUtc,
        int failedDays,
        int totalOrders,
        int totalGraphQlErrors,
        IReadOnlyList<object> days);
    FlowEngineOperationExecutionData CreateFetchReturnsResult(
        JeevesRuntimeContext runtimeContext,
        string selectionKind,
        IReadOnlyList<DateTime> dates,
        DateTime sinceUtc,
        DateTime untilUtc,
        int failedDays,
        int totalReturns,
        int totalGraphQlErrors,
        IReadOnlyList<object> days);
}

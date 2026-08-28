using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineShopifyCompleteOrdersResultFactory
{
    FlowEngineShopifyCompleteOrderPayload CreateSinglePayload(string orderId, string? orderGid, bool dryRun, bool closeOrder, FlowEngineShopifyCompleteOrderRow result);
    FlowEngineOperationExecutionData BuildSingleOrderExecution(FlowEngineShopifyCompleteOrderPayload payload, string companyName, int companyCode, string storeDomain);
    FlowEngineShopifyCompleteOrdersPayload CreateBulkPayload(string? date, string? sinceUtc, string? untilUtc, bool useLatestDay, string selectionKind, bool dryRun, bool closeOrder);
    void IncrementCounts(FlowEngineShopifyCompleteOrdersPendingCounts counts, string status);
    void MergeCounts(FlowEngineShopifyCompleteOrdersPendingCounts target, FlowEngineShopifyCompleteOrdersPendingCounts source);
    FlowEngineOperationExecutionData BuildBulkExecution(FlowEngineShopifyCompleteOrdersPayload payload, string operationLabel, string modeLabel, string companyName, int companyCode, string storeDomain);
}

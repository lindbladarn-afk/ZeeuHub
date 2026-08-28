using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineShopifyReadResultFactory
{
    FlowEngineOperationExecutionData BuildScopesCheckExecution(
        FlowEngineShopifyScopeProbeResult scopeProbe,
        IReadOnlyList<FlowEngineShopifyScopeProbeCategory> categories,
        string storeDomain);

    FlowEngineOperationExecutionData BuildGetProductsExecution(
        string companyName,
        int companyCode,
        string storeDomain,
        int effectiveLimit,
        string? updatedSince,
        string? searchQuery,
        bool includeInventoryItem,
        bool includeMetafields,
        FlowEngineShopifyCollectProductsResult pageResult,
        IReadOnlyList<ShopifyProductNode> orderedProducts);

    FlowEngineOperationExecutionData BuildFetchOrderExecution(
        string numericId,
        string orderGid,
        ShopifyOrderDetailNode order,
        string storeDomain);

    FlowEngineOperationExecutionData BuildFetchOrdersExecution(
        string? date,
        string sinceUtc,
        string untilUtc,
        bool useLatestDay,
        string selectionKind,
        IReadOnlyList<object> days,
        int totalOrders,
        string storeDomain,
        string selectionSummaryLabel);

    FlowEngineOperationExecutionData BuildValidateOrderExecution(
        string orderId,
        string? orderGid,
        FlowEngineShopifyValidationDecision validation,
        string storeDomain);

    FlowEngineShopifyValidateOrdersPayload CreateValidateOrdersPayload(
        string? date,
        string sinceUtc,
        string untilUtc,
        bool useLatestDay,
        string selectionKind);

    FlowEngineShopifyValidateOrdersDayPayload CreateValidateOrdersDay(string date);

    FlowEngineOperationExecutionData BuildValidateOrdersExecution(
        FlowEngineShopifyValidateOrdersPayload payload,
        string selectionSummaryLabel,
        string storeDomain);

    FlowEngineShopifyCheckOrdersPayload CreateCheckOrdersPayload(
        string? date,
        string sinceUtc,
        string untilUtc,
        bool useLatestDay,
        string selectionKind);

    FlowEngineShopifyCheckOrdersDayPayload CreateCheckOrdersDay(string date);

    FlowEngineOperationExecutionData BuildCheckOrdersExecution(
        FlowEngineShopifyCheckOrdersPayload payload,
        string selectionSummaryLabel,
        string storeDomain);

    FlowEngineOperationExecutionData BuildSendOrderExecution(
        string storeDomain,
        bool dryRun,
        bool skipJeevesCheck,
        string orderId,
        string? orderGid,
        string status,
        FlowEngineShopifyValidationDecision? validation,
        FlowEngineShopifyJeevesOrderPayload? mappedPayload,
        string? errorMessage);

    FlowEngineShopifySendOrdersPayload CreateSendOrdersPayload(
        string? date,
        string sinceUtc,
        string untilUtc,
        bool useLatestDay,
        string selectionKind,
        bool dryRun,
        bool skipJeevesCheck);

    FlowEngineShopifySendOrdersDayPayload CreateSendOrdersDay(string date);

    void AddSendOrderOutcome(FlowEngineShopifySendOrdersDayPayload day, string orderId, string? orderNumber, string? orderGid, string status, FlowEngineShopifyValidationDecision? validation, string? errorMessage);

    FlowEngineOperationExecutionData BuildSendOrdersExecution(
        FlowEngineShopifySendOrdersPayload payload,
        string selectionSummaryLabel,
        string storeDomain);
}

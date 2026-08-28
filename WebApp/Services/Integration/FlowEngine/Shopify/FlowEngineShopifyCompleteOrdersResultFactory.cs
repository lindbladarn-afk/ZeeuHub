using System.Text.Json;
using System.Text.Json.Serialization;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineShopifyCompleteOrdersResultFactory : IFlowEngineShopifyCompleteOrdersResultFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public FlowEngineShopifyCompleteOrderPayload CreateSinglePayload(string orderId, string? orderGid, bool dryRun, bool closeOrder, FlowEngineShopifyCompleteOrderRow result)
        => new()
        {
            OrderId = orderId,
            OrderGid = orderGid,
            DryRun = dryRun,
            CloseOrder = closeOrder,
            Result = result
        };

    public FlowEngineOperationExecutionData BuildSingleOrderExecution(FlowEngineShopifyCompleteOrderPayload payload, string companyName, int companyCode, string storeDomain)
    {
        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Shopify complete order {payload.OrderId}: Status={payload.Result.Status}",
                $"Portalbolag: {companyName} ({companyCode})",
                $"Mode: {(payload.DryRun ? "dry run" : "complete order")}",
                $"Close order: {(payload.CloseOrder ? "true" : "false")}",
                $"Shopify store: {storeDomain}"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

    public FlowEngineShopifyCompleteOrdersPayload CreateBulkPayload(string? date, string? sinceUtc, string? untilUtc, bool useLatestDay, string selectionKind, bool dryRun, bool closeOrder)
        => new()
        {
            Date = date,
            SinceUtc = sinceUtc,
            UntilUtc = untilUtc,
            UseLatestDay = useLatestDay,
            SelectionKind = selectionKind,
            DryRun = dryRun,
            CloseOrder = closeOrder
        };

    public void IncrementCounts(FlowEngineShopifyCompleteOrdersPendingCounts counts, string status)
    {
        switch (status)
        {
            case "completed":
                counts.Completed++;
                break;
            case "ready":
                counts.Ready++;
                break;
            case "skipped_already_complete":
                counts.SkippedAlreadyComplete++;
                break;
            case "skipped_ineligible":
                counts.SkippedIneligible++;
                break;
            case "failed":
                counts.Failed++;
                break;
        }
    }

    public void MergeCounts(FlowEngineShopifyCompleteOrdersPendingCounts target, FlowEngineShopifyCompleteOrdersPendingCounts source)
    {
        target.Total += source.Total;
        target.Completed += source.Completed;
        target.Ready += source.Ready;
        target.SkippedAlreadyComplete += source.SkippedAlreadyComplete;
        target.SkippedIneligible += source.SkippedIneligible;
        target.Failed += source.Failed;
    }

    public FlowEngineOperationExecutionData BuildBulkExecution(FlowEngineShopifyCompleteOrdersPayload payload, string operationLabel, string modeLabel, string companyName, int companyCode, string storeDomain)
    {
        var selectionSummary = BuildSelectionSummaryLabel(payload);

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"{operationLabel} {selectionSummary}: Total={payload.Counts.Total}, Completed={payload.Counts.Completed}, Ready={payload.Counts.Ready}, SkippedAlreadyComplete={payload.Counts.SkippedAlreadyComplete}, SkippedIneligible={payload.Counts.SkippedIneligible}, Failed={payload.Counts.Failed}",
                $"Portalbolag: {companyName} ({companyCode})",
                $"Mode: {(payload.DryRun ? "dry run" : modeLabel)}",
                $"Close order: {(payload.CloseOrder ? "true" : "false")}",
                $"Shopify store: {storeDomain}"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

    private static string BuildSelectionSummaryLabel(FlowEngineShopifyCompleteOrdersPayload payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.Date))
            return payload.Date;

        if (!string.IsNullOrWhiteSpace(payload.SinceUtc) && !string.IsNullOrWhiteSpace(payload.UntilUtc))
            return payload.SinceUtc == payload.UntilUtc ? payload.SinceUtc : $"{payload.SinceUtc} -> {payload.UntilUtc}";

        return "n/a";
    }
}

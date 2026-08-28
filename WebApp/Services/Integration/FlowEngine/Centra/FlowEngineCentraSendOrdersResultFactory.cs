using System.Text.Json;
using System.Text.Json.Serialization;
using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraSendOrdersResultFactory : IFlowEngineCentraSendOrdersResultFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public FlowEngineOperationExecutionData CreateBulkResult(
        JeevesRuntimeContext runtimeContext,
        string date,
        int? limit,
        bool dryRun,
        bool skipJeevesCheck,
        FlowEngineSendOrdersCounts counts,
        double runtimeSeconds,
        IReadOnlyList<FlowEngineSendOrdersRow> nonCleanRows)
    {
        var payload = new FlowEngineSendOrdersPayload
        {
            Date = date,
            DryRun = dryRun,
            Counts = counts,
            RuntimeSeconds = runtimeSeconds,
            Orders = nonCleanRows.ToList()
        };

        var limitSummary = limit.HasValue && limit.Value > 0 ? $" (limit {limit.Value})" : string.Empty;
        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Centra send orders {payload.Date}{limitSummary}: Centra={counts.CentraTotal}, Mapped={counts.Mapped}, SkippedExisting={counts.SkippedExisting}, SkippedIneligible={counts.SkippedIneligible}, ManualReview={counts.ManualReviewRequired}, Failed={counts.Failed}, Runtime={payload.RuntimeSeconds:0.00}s",
                $"Portalbolag: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})",
                $"Mode: {(dryRun ? "dry run" : "send to Jeeves")} | Skip Jeeves check: {(skipJeevesCheck ? "yes" : "no")}",
                "Jeeves target company code: 1"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

    public FlowEngineOperationExecutionData CreateSingleResult(
        JeevesRuntimeContext runtimeContext,
        string orderId,
        bool dryRun,
        bool skipJeevesCheck,
        FlowEngineSendOrdersRow result)
    {
        JsonElement? mappedPayload = null;
        if (!string.IsNullOrWhiteSpace(result.PayloadJson))
        {
            using var document = JsonDocument.Parse(result.PayloadJson);
            mappedPayload = document.RootElement.Clone();
        }

        var payload = new FlowEngineSendOrderSinglePayload
        {
            OrderId = orderId,
            DryRun = dryRun,
            SkipJeevesCheck = skipJeevesCheck,
            Result = result,
            Payload = mappedPayload
        };

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Centra send order {orderId}: Status={result.Status}",
                $"Portalbolag: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})",
                $"Mode: {(dryRun ? "dry run" : "send to Jeeves")} | Skip Jeeves check: {(skipJeevesCheck ? "yes" : "no")}",
                "Jeeves target company code: 1"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

}

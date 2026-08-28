using System.Text.Json;
using System.Text.Json.Serialization;
using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraSendReturnsResultFactory : IFlowEngineCentraSendReturnsResultFactory
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
        FlowEngineSendReturnsCounts counts,
        double runtimeSeconds,
        IReadOnlyList<FlowEngineSendReturnsRow> nonCleanRows)
    {
        var payload = new FlowEngineSendReturnsPayload
        {
            Date = date,
            DryRun = dryRun,
            Counts = counts,
            RuntimeSeconds = runtimeSeconds,
            Returns = nonCleanRows.ToList()
        };

        var limitSummary = limit.HasValue && limit.Value > 0 ? $" (limit {limit.Value})" : string.Empty;
        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Centra send returns {payload.Date}{limitSummary}: Centra={counts.CentraTotal}, Mapped={counts.Mapped}, SkippedIneligible={counts.SkippedIneligible}, FailedValidation={counts.FailedValidation}, FailedMapping={counts.FailedMapping}, FailedApi={counts.FailedApi}, AlreadyExists={counts.AlreadyExists}, Runtime={payload.RuntimeSeconds:0.00}s",
                $"Portalbolag: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})",
                $"Mode: {(dryRun ? "dry run" : "send to Jeeves")}",
                "Jeeves target company code: 1"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

    public FlowEngineOperationExecutionData CreateSingleResult(
        JeevesRuntimeContext runtimeContext,
        int returnId,
        bool dryRun,
        FlowEngineSendReturnsRow result)
    {
        var payload = new
        {
            returnId,
            dryRun,
            result
        };

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Centra send return {returnId}: Status={result.Status}",
                $"Portalbolag: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})",
                $"Mode: {(dryRun ? "dry run" : "send to Jeeves")}",
                "Jeeves target company code: 1"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

}

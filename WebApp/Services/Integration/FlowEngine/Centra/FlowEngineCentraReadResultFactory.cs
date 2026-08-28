using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraReadResultFactory : IFlowEngineCentraReadResultFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public FlowEngineOperationExecutionData CreateFetchOrderResult(JeevesRuntimeContext runtimeContext, string orderId, string body)
    {
        var prettyBody = PrettyPrintJson(body);
        var found = CountSingleDataObject(prettyBody, "order");

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Centra fetch-order {orderId}: Matches={found}",
                $"Portalbolag: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})",
                "Output: raw GraphQL payload"
            },
            JsonOutput = prettyBody
        };
    }

    public FlowEngineOperationExecutionData CreateFetchReturnResult(JeevesRuntimeContext runtimeContext, int returnId, string body)
    {
        var prettyBody = PrettyPrintJson(body);
        var found = CountDataArray(prettyBody, "returns");

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Centra fetch-return {returnId}: Matches={found}",
                $"Portalbolag: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})",
                "Output: raw GraphQL payload"
            },
            JsonOutput = prettyBody
        };
    }

    public FlowEngineOperationExecutionData CreateFetchOrdersResult(
        JeevesRuntimeContext runtimeContext,
        string selectionKind,
        IReadOnlyList<DateTime> dates,
        DateTime sinceUtc,
        DateTime untilUtc,
        int failedDays,
        int totalOrders,
        int totalGraphQlErrors,
        IReadOnlyList<object> days)
    {
        var payload = new
        {
            selectionKind,
            date = dates.Count == 1 ? FormatDateUtc(dates[0]) : null,
            sinceUtc = FormatDateUtc(sinceUtc),
            untilUtc = FormatDateUtc(untilUtc),
            useLatestDay = selectionKind == "latest-day",
            counts = new
            {
                days = dates.Count,
                failedDays,
                fetched = totalOrders,
                graphQlErrors = totalGraphQlErrors
            },
            days
        };

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Centra fetch-orders {BuildSelectionSummaryLabel(payload.selectionKind, payload.date, payload.sinceUtc, payload.untilUtc)}: Days={payload.counts.days}, Orders={payload.counts.fetched}, FailedDays={payload.counts.failedDays}, GraphQlErrors={payload.counts.graphQlErrors}",
                $"Portalbolag: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})",
                "Output: per dag med raw GraphQL data.orders och eventuella errors"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

    public FlowEngineOperationExecutionData CreateFetchReturnsResult(
        JeevesRuntimeContext runtimeContext,
        string selectionKind,
        IReadOnlyList<DateTime> dates,
        DateTime sinceUtc,
        DateTime untilUtc,
        int failedDays,
        int totalReturns,
        int totalGraphQlErrors,
        IReadOnlyList<object> days)
    {
        var payload = new
        {
            selectionKind,
            date = dates.Count == 1 ? FormatDateUtc(dates[0]) : null,
            sinceUtc = FormatDateUtc(sinceUtc),
            untilUtc = FormatDateUtc(untilUtc),
            useLatestDay = selectionKind == "latest-day",
            counts = new
            {
                days = dates.Count,
                failedDays,
                fetched = totalReturns,
                graphQlErrors = totalGraphQlErrors
            },
            days
        };

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Centra fetch-returns {BuildSelectionSummaryLabel(payload.selectionKind, payload.date, payload.sinceUtc, payload.untilUtc)}: Days={payload.counts.days}, Returns={payload.counts.fetched}, FailedDays={payload.counts.failedDays}, GraphQlErrors={payload.counts.graphQlErrors}",
                $"Portalbolag: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})",
                "Output: per dag med raw GraphQL data.returns och eventuella errors"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

    private static string FormatDateUtc(DateTime value)
        => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string BuildSelectionSummaryLabel(string selectionKind, string? date, string sinceUtc, string untilUtc)
    {
        return selectionKind switch
        {
            "latest-day" => "latest-day",
            "range" => $"{sinceUtc} -> {untilUtc}",
            _ => date ?? sinceUtc
        };
    }

    private static int CountSingleDataObject(string json, string objectFieldName)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Object)
            return 0;

        if (!dataElement.TryGetProperty(objectFieldName, out var objectElement) || objectElement.ValueKind == JsonValueKind.Null)
            return 0;

        return objectElement.ValueKind == JsonValueKind.Object ? 1 : 0;
    }

    private static int CountDataArray(string json, string arrayFieldName)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Object)
            return 0;

        if (!dataElement.TryGetProperty(arrayFieldName, out var arrayElement) || arrayElement.ValueKind != JsonValueKind.Array)
            return 0;

        return arrayElement.GetArrayLength();
    }

    private static string PrettyPrintJson(string body)
    {
        using var document = JsonDocument.Parse(body);
        return JsonSerializer.Serialize(document.RootElement, JsonOptions);
    }
}

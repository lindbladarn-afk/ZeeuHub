using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.Services.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraCreateShipmentsService : IFlowEngineCentraCreateShipmentsService
{
    private const int CentraOriginJeevesCompanyCode = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly IFlowEngineCentraConnectionService _centraConnectionService;
    private readonly IFlowEngineCentraJeevesBridgeService _centraJeevesBridgeService;
    private readonly FlowEngineCentraShipmentLookupService _shipmentLookupService;
    private readonly FlowEngineCentraShipmentJeevesStatusService _shipmentJeevesStatusService;
    private readonly FlowEngineCentraShipmentWorkflowService _shipmentWorkflowService;

    public FlowEngineCentraCreateShipmentsService(
        IFlowEngineCentraConnectionService centraConnectionService,
        IFlowEngineCentraJeevesBridgeService centraJeevesBridgeService,
        FlowEngineCentraShipmentLookupService shipmentLookupService,
        FlowEngineCentraShipmentJeevesStatusService shipmentJeevesStatusService,
        FlowEngineCentraShipmentWorkflowService shipmentWorkflowService)
    {
        _centraConnectionService = centraConnectionService;
        _centraJeevesBridgeService = centraJeevesBridgeService;
        _shipmentLookupService = shipmentLookupService;
        _shipmentJeevesStatusService = shipmentJeevesStatusService;
        _shipmentWorkflowService = shipmentWorkflowService;
    }

    public async Task<FlowEngineOperationExecutionData> ExecuteAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken = default)
    {
        var isSingle = request.Operation == FlowEngineOperationType.CreateShipment;
        var isPendingBatch = request.Operation == FlowEngineOperationType.CreateShipmentsPending;
        var utcToday = DateTime.UtcNow.Date;
        var targetDateUtc = isPendingBatch
            ? utcToday
            : ResolveTargetDateUtc(request.Params.DateUtc);
        var dayStartUtc = DateTime.SpecifyKind(targetDateUtc.Date, DateTimeKind.Utc);
        var limit = request.Params.UseLimit ? request.Params.Limit : null;
        var dryRun = request.Flags.DryRun;

        var centraConfig = _centraConnectionService.ResolveConfig(runtimeContext.CompanyId, "create shipments", request.Flags.TestMode);
        var jeevesConfig = _centraJeevesBridgeService.ResolveConfig(runtimeContext.CompanyId, "create shipments");
        var stopwatch = Stopwatch.StartNew();

        if (isSingle)
        {
            var singleResult = await _shipmentWorkflowService.ExecuteSingleShipmentAsync(runtimeContext, request, centraConfig, jeevesConfig, dryRun, cancellationToken);
            stopwatch.Stop();
            return new FlowEngineOperationExecutionData
            {
                SummaryLines =
                {
                    $"Centra create shipment {singleResult.OrderId}: Success={singleResult.Result.Success}, Runtime={stopwatch.Elapsed.TotalSeconds:0.00}s",
                    $"Portalbolag: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})",
                    $"Mode: {(dryRun ? "dry run" : "create shipment")}",
                    $"Jeeves target company code: {CentraOriginJeevesCompanyCode}"
                },
                JsonOutput = JsonSerializer.Serialize(singleResult, JsonOptions)
            };
        }

        var orders = isPendingBatch
            ? await _shipmentLookupService.FetchShipmentOrdersByStatusesAsync(centraConfig, new[] { "CONFIRMED", "PROCESSING" }, cancellationToken)
            : await _shipmentLookupService.FetchShipmentOrdersByDateAsync(centraConfig, dayStartUtc, cancellationToken);
        var ordered = orders
            .OrderBy(order => order.CreatedAt ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(order => order.OrderId, StringComparer.Ordinal)
            .ToList();

        if (limit.HasValue && limit.Value > 0)
            ordered = ordered.Take(limit.Value).ToList();

        var counts = new FlowEngineCreateShipmentsCounts
        {
            CentraTotal = ordered.Count
        };

        var results = new List<FlowEngineCreateShipmentResultRow>(ordered.Count);
        Dictionary<string, JeevesOrderCheckResult> jeevesChecks = new(StringComparer.Ordinal);

        var preflight = new Dictionary<string, FlowEngineCreateShipmentResultRow>(StringComparer.Ordinal);
        var toCheckInJeeves = new List<ShipmentOrderContext>();

        foreach (var order in ordered)
        {
            switch (FlowEngineCentraCreateShipmentsHelper.ClassifyBatchShipmentPreflight(order))
            {
                case BatchShipmentPreflightClassification.SkipAlreadyComplete:
                    counts.SkippedIneligible++;
                    preflight[order.OrderId] = FlowEngineCentraCreateShipmentsHelper.FailedShipmentResult(
                        order.OrderId,
                        order.OrderNumber,
                        order.StoreId,
                        FlowEngineCentraCreateShipmentsHelper.BatchAlreadyCompleteSkipMessage(order.Status),
                        Array.Empty<FlowEngineShipmentLineInput>(),
                        Array.Empty<string>());
                    break;
                case BatchShipmentPreflightClassification.SkipBatchExpectedConfirmed:
                    counts.SkippedIneligible++;
                    preflight[order.OrderId] = FlowEngineCentraCreateShipmentsHelper.FailedShipmentResult(
                        order.OrderId,
                        order.OrderNumber,
                        order.StoreId,
                        FlowEngineCentraCreateShipmentsHelper.BatchExpectedConfirmedSkipMessage(order.Status),
                        Array.Empty<FlowEngineShipmentLineInput>(),
                        Array.Empty<string>());
                    break;
                case BatchShipmentPreflightClassification.ProceedToJeeves:
                    toCheckInJeeves.Add(order);
                    break;
            }
        }

        if (toCheckInJeeves.Count > 0)
            jeevesChecks = await _shipmentJeevesStatusService.CheckOrdersAsync(runtimeContext.CompanyId, jeevesConfig, toCheckInJeeves, cancellationToken);

        foreach (var order in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (preflight.TryGetValue(order.OrderId, out var preflightResult))
            {
                results.Add(preflightResult);
                continue;
            }

            if (!jeevesChecks.TryGetValue(order.OrderId, out var jeevesCheck))
            {
                var result = FlowEngineCentraCreateShipmentsHelper.FailedShipmentResult(
                    order.OrderId,
                    order.OrderNumber,
                    order.StoreId,
                    "Jeeves check missing for order",
                    Array.Empty<FlowEngineShipmentLineInput>(),
                    Array.Empty<string>());
                results.Add(result);
                counts.Failed++;
                continue;
            }

            switch (jeevesCheck.Status)
            {
                case JeevesCheckStatus.NotFound:
                    counts.SkippedIneligible++;
                    results.Add(FlowEngineCentraCreateShipmentsHelper.FailedShipmentResult(
                        order.OrderId,
                        order.OrderNumber,
                        order.StoreId,
                        "Skipped: order not found in Jeeves",
                        Array.Empty<FlowEngineShipmentLineInput>(),
                        Array.Empty<string>()));
                    continue;
                case JeevesCheckStatus.Error:
                    counts.Failed++;
                    results.Add(FlowEngineCentraCreateShipmentsHelper.FailedShipmentResult(
                        order.OrderId,
                        order.OrderNumber,
                        order.StoreId,
                        jeevesCheck.StatusName ?? "Jeeves check failed",
                        Array.Empty<FlowEngineShipmentLineInput>(),
                        Array.Empty<string>()));
                    continue;
                case JeevesCheckStatus.Found:
                    break;
            }

            if (jeevesCheck.JeevesOrderStatus < 50)
            {
                counts.SkippedIneligible++;
                results.Add(FlowEngineCentraCreateShipmentsHelper.FailedShipmentResult(
                    order.OrderId,
                    order.OrderNumber,
                    order.StoreId,
                    "Skipped: Jeeves c_ordstat must be >= 50",
                    Array.Empty<FlowEngineShipmentLineInput>(),
                    Array.Empty<string>()));
                continue;
            }

            counts.Eligible++;
            var outcome = await _shipmentWorkflowService.CreateShipmentForEligibleOrderAsync(order, dryRun, centraConfig, cancellationToken);
            results.Add(outcome.Result);
            switch (outcome.State)
            {
                case ShipmentProcessState.Success:
                    counts.Successful++;
                    break;
                case ShipmentProcessState.Skipped:
                    counts.SkippedIneligible++;
                    break;
                case ShipmentProcessState.Failed:
                    counts.Failed++;
                    break;
            }
        }

        stopwatch.Stop();

        var payloadDate = isPendingBatch
            ? $"pending-{utcToday:yyyy-MM-dd}"
            : dayStartUtc.ToString("yyyy-MM-dd");
        var commandLabel = isPendingBatch
            ? "Centra create shipments pending"
            : "Centra create shipments";

        var payload = new FlowEngineCreateShipmentsPayload
        {
            Date = payloadDate,
            DryRun = dryRun,
            Counts = counts,
            Results = results
        };

        var limitSummary = limit.HasValue && limit.Value > 0 ? $" (limit {limit.Value})" : string.Empty;
        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"{commandLabel} {payload.Date}{limitSummary}: Centra={counts.CentraTotal}, Eligible={counts.Eligible}, Successful={counts.Successful}, SkippedIneligible={counts.SkippedIneligible}, Failed={counts.Failed}, Runtime={stopwatch.Elapsed.TotalSeconds:0.00}s",
                $"Portalbolag: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})",
                $"Mode: {(dryRun ? "dry run" : (isPendingBatch ? "create shipments pending" : "create shipments"))}",
                $"Jeeves target company code: {CentraOriginJeevesCompanyCode}"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

    private static DateTime ResolveTargetDateUtc(string? dateUtc)
    {
        if (string.IsNullOrWhiteSpace(dateUtc))
            return DateTime.UtcNow.Date;

        if (!DateTime.TryParse(dateUtc, out var parsed))
            throw new InvalidOperationException("Datum maste anges i formatet yyyy-MM-dd for Centra create shipments.");

        return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
    }

}

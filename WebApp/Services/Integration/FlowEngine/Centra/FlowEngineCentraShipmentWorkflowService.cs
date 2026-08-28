using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraShipmentWorkflowService
{
    private readonly FlowEngineCentraShipmentLookupService _shipmentLookupService;
    private readonly FlowEngineCentraShipmentJeevesStatusService _shipmentJeevesStatusService;
    private readonly FlowEngineCentraShipmentMutationService _shipmentMutationService;

    public FlowEngineCentraShipmentWorkflowService(
        FlowEngineCentraShipmentLookupService shipmentLookupService,
        FlowEngineCentraShipmentJeevesStatusService shipmentJeevesStatusService,
        FlowEngineCentraShipmentMutationService shipmentMutationService)
    {
        _shipmentLookupService = shipmentLookupService;
        _shipmentJeevesStatusService = shipmentJeevesStatusService;
        _shipmentMutationService = shipmentMutationService;
    }

    internal async Task<FlowEngineCreateShipmentPayload> ExecuteSingleShipmentAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        IntegrationSourceConfig centraConfig,
        IntegrationSourceConfig jeevesConfig,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var orderId = request.Params.OrderId?.Trim();
        if (string.IsNullOrWhiteSpace(orderId))
            throw new InvalidOperationException("Order ID maste anges for Centra create shipment.");

        var order = await _shipmentLookupService.FetchShipmentOrderByIdAsync(centraConfig, orderId, cancellationToken);
        if (order is null)
        {
            return new FlowEngineCreateShipmentPayload
            {
                OrderId = orderId,
                DryRun = dryRun,
                Result = FlowEngineCentraCreateShipmentsHelper.FailedShipmentResult(orderId, string.Empty, null, "Order not found in Centra", Array.Empty<FlowEngineShipmentLineInput>(), Array.Empty<string>())
            };
        }

        if (FlowEngineCentraCreateShipmentsHelper.ShouldSkipAlreadyComplete(order.Status, order.ShipmentFlags))
        {
            return new FlowEngineCreateShipmentPayload
            {
                OrderId = order.OrderId,
                DryRun = dryRun,
                Result = FlowEngineCentraCreateShipmentsHelper.FailedShipmentResult(order.OrderId, order.OrderNumber, order.StoreId, FlowEngineCentraCreateShipmentsHelper.BatchAlreadyCompleteSkipMessage(order.Status), Array.Empty<FlowEngineShipmentLineInput>(), Array.Empty<string>())
            };
        }

        var jeevesCheck = await _shipmentJeevesStatusService.CheckOrderAsync(runtimeContext.CompanyId, jeevesConfig, order.OrderId, cancellationToken);

        var result = jeevesCheck.Status switch
        {
            JeevesCheckStatus.NotFound => FlowEngineCentraCreateShipmentsHelper.FailedShipmentResult(order.OrderId, order.OrderNumber, order.StoreId, "Skipped: order not found in Jeeves", Array.Empty<FlowEngineShipmentLineInput>(), Array.Empty<string>()),
            JeevesCheckStatus.Error => FlowEngineCentraCreateShipmentsHelper.FailedShipmentResult(order.OrderId, order.OrderNumber, order.StoreId, jeevesCheck.StatusName ?? "Jeeves check failed", Array.Empty<FlowEngineShipmentLineInput>(), Array.Empty<string>()),
            _ when jeevesCheck.JeevesOrderStatus < 50 => FlowEngineCentraCreateShipmentsHelper.FailedShipmentResult(order.OrderId, order.OrderNumber, order.StoreId, "Skipped: Jeeves c_ordstat must be >= 50", Array.Empty<FlowEngineShipmentLineInput>(), Array.Empty<string>()),
            _ => (await CreateShipmentForEligibleOrderAsync(order, dryRun, centraConfig, cancellationToken)).Result
        };

        return new FlowEngineCreateShipmentPayload
        {
            OrderId = order.OrderId,
            DryRun = dryRun,
            Result = result
        };
    }

    internal async Task<ShipmentProcessOutcome> CreateShipmentForEligibleOrderAsync(
        ShipmentOrderContext order,
        bool dryRun,
        IntegrationSourceConfig centraConfig,
        CancellationToken cancellationToken)
    {
        var preparation = FlowEngineCentraCreateShipmentsHelper.PrepareLines(order.RawOrder);
        var warnings = new List<string>();
        var shipmentLines = preparation.LinesForShipment;
        var cancellationLines = preparation.LinesForCancellation;

        if (dryRun)
        {
            if (!string.Equals(FlowEngineCentraCreateShipmentsHelper.NormalizeStatus(order.Status), "SHIPPED", StringComparison.Ordinal) && cancellationLines.Count > 0)
            {
                var summary = FlowEngineCentraCreateShipmentsHelper.CancellationSummaryMessage(order.OrderId, cancellationLines, true);
                if (!string.IsNullOrWhiteSpace(summary))
                    warnings.Add(summary);
            }

            if (shipmentLines.Count == 0)
            {
                return new ShipmentProcessOutcome(
                    FlowEngineCentraCreateShipmentsHelper.FailedShipmentResult(order.OrderId, order.OrderNumber, order.StoreId, "No lines available for shipment after allocation checks", shipmentLines, warnings),
                    ShipmentProcessState.Failed);
            }

            warnings.Insert(0, "Dry-run: shipment not created");
            return new ShipmentProcessOutcome(
                new FlowEngineCreateShipmentResultRow
                {
                    OrderId = order.OrderId,
                    OrderNumber = order.OrderNumber,
                    Success = true,
                    StoreId = order.StoreId,
                    Warnings = warnings,
                    OrderLines = shipmentLines
                },
                ShipmentProcessState.Success);
        }

        if (cancellationLines.Count > 0 && !string.Equals(FlowEngineCentraCreateShipmentsHelper.NormalizeStatus(order.Status), "SHIPPED", StringComparison.Ordinal))
        {
            var summary = FlowEngineCentraCreateShipmentsHelper.CancellationSummaryMessage(order.OrderId, cancellationLines, false);
            if (!string.IsNullOrWhiteSpace(summary))
                warnings.Add(summary);

            var cancellation = await _shipmentMutationService.CancelUnallocatedLinesAsync(order, cancellationLines, centraConfig, cancellationToken);
            if (!cancellation.Success)
            {
                var message = cancellation.ErrorMessage ?? "Cancellation failed before shipment create";
                if (FlowEngineCentraCreateShipmentsHelper.ShouldHardStopAfterCancellationFailure(order.StoreId))
                {
                    return new ShipmentProcessOutcome(
                        FlowEngineCentraCreateShipmentsHelper.FailedShipmentResult(order.OrderId, order.OrderNumber, order.StoreId, message, shipmentLines, warnings),
                        ShipmentProcessState.Failed);
                }

                warnings.Add(message);
            }
        }

        if (shipmentLines.Count == 0)
        {
            return new ShipmentProcessOutcome(
                FlowEngineCentraCreateShipmentsHelper.FailedShipmentResult(order.OrderId, order.OrderNumber, order.StoreId, "No lines available for shipment after allocation checks", shipmentLines, warnings),
                ShipmentProcessState.Failed);
        }

        ShipmentMutationOutcome workflowResult;
        switch (order.StoreId)
        {
            case 1:
                workflowResult = await _shipmentMutationService.RunStore1WorkflowAsync(order, shipmentLines, centraConfig, cancellationToken);
                break;
            case 2:
            case 4:
                workflowResult = await _shipmentMutationService.RunStore24WorkflowAsync(order, shipmentLines, centraConfig, cancellationToken);
                break;
            default:
                workflowResult = await _shipmentMutationService.RunDefaultWorkflowAsync(order, shipmentLines, centraConfig, cancellationToken);
                break;
        }

        warnings.AddRange(workflowResult.Warnings);
        if (workflowResult.Success)
        {
            return new ShipmentProcessOutcome(
                new FlowEngineCreateShipmentResultRow
                {
                    OrderId = order.OrderId,
                    OrderNumber = order.OrderNumber,
                    Success = true,
                    ShipmentId = workflowResult.Shipment?.Id,
                    IsCaptured = workflowResult.Shipment?.IsCaptured,
                    IsShipped = workflowResult.Shipment?.IsShipped,
                    StoreId = order.StoreId,
                    Warnings = warnings,
                    OrderLines = shipmentLines
                },
                ShipmentProcessState.Success);
        }

        return new ShipmentProcessOutcome(
            FlowEngineCentraCreateShipmentsHelper.FailedShipmentResult(order.OrderId, order.OrderNumber, order.StoreId, workflowResult.ErrorMessage ?? "Shipment creation failed", shipmentLines, warnings),
            workflowResult.AlreadyShipped ? ShipmentProcessState.Skipped : ShipmentProcessState.Failed);
    }
}

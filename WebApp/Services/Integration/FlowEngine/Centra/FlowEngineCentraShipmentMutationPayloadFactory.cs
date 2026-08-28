using System.Globalization;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraShipmentMutationPayloadFactory
{
    private readonly IFlowEngineCentraQueryCatalog _centraQueryCatalog;

    public FlowEngineCentraShipmentMutationPayloadFactory(IFlowEngineCentraQueryCatalog centraQueryCatalog)
    {
        _centraQueryCatalog = centraQueryCatalog;
    }

    internal IReadOnlyList<CancellationAttempt> BuildCancellationAttempts(int storeId, IReadOnlyList<FlowEngineShipmentLineInput> lines)
    {
        var dtcWithStockAction = new CancellationAttempt(
            "DTC+StockAction",
            _centraQueryCatalog.GetCancelOrderLinesDirectToConsumerQuery(lines, includeStockAction: true),
            "updateDtcCancel");
        var dtcWithoutStockAction = new CancellationAttempt(
            "DTCFallbackNoStockAction",
            _centraQueryCatalog.GetCancelOrderLinesDirectToConsumerQuery(lines, includeStockAction: false),
            "updateDtcCancel");
        var wholesaleWithStockAction = new CancellationAttempt(
            "Wholesale+StockAction",
            _centraQueryCatalog.GetCancelOrderLinesWholesaleQuery(lines, includeStockAction: true),
            "updateWholesaleCancel");
        var wholesaleWithoutStockAction = new CancellationAttempt(
            "WholesaleFallbackNoStockAction",
            _centraQueryCatalog.GetCancelOrderLinesWholesaleQuery(lines, includeStockAction: false),
            "updateWholesaleCancel");

        return storeId == 2
            ? new[] { dtcWithStockAction, dtcWithoutStockAction, wholesaleWithStockAction, wholesaleWithoutStockAction }
            : new[] { wholesaleWithoutStockAction };
    }

    internal object BuildCreateShipmentPayload(string orderId, IReadOnlyList<FlowEngineShipmentLineInput> lines, bool? captureOverride)
    {
        var query = captureOverride.HasValue
            ? _centraQueryCatalog.GetCreateShipmentWithCaptureQuery()
            : _centraQueryCatalog.GetCreateShipmentQuery();

        return captureOverride.HasValue
            ? new
            {
                query,
                variables = new
                {
                    orderId,
                    lines = FlowEngineCentraCreateShipmentsHelper.BuildShipmentLines(lines),
                    capture = captureOverride.Value
                },
                operationName = "createShipmentWithCapturing"
            }
            : new
            {
                query,
                variables = new
                {
                    orderId,
                    lines = FlowEngineCentraCreateShipmentsHelper.BuildShipmentLines(lines)
                },
                operationName = "createShipment"
            };
    }

    internal bool TryBuildCapturePayload(string shipmentId, out object? payload, out string? errorMessage)
    {
        payload = null;
        errorMessage = null;

        if (!int.TryParse(shipmentId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId))
        {
            errorMessage = "Invalid shipment id for capture";
            return false;
        }

        payload = new
        {
            query = _centraQueryCatalog.GetCaptureShipmentQuery(),
            variables = new { id = numericId },
            operationName = "captureShipment"
        };
        return true;
    }

    internal bool TryBuildCompletePayload(string shipmentId, bool sendEmail, out object? payload, out string? errorMessage)
    {
        payload = null;
        errorMessage = null;

        if (!int.TryParse(shipmentId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId))
        {
            errorMessage = "Invalid shipment id for complete";
            return false;
        }

        payload = new
        {
            query = _centraQueryCatalog.GetCompleteShipmentQuery(),
            variables = new { id = numericId, sendEmail },
            operationName = "completeShipment"
        };
        return true;
    }

    internal bool TryBuildMarkPaidPayload(string shipmentId, out object? payload, out string? errorMessage)
    {
        payload = null;
        errorMessage = null;

        if (!int.TryParse(shipmentId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId))
        {
            errorMessage = "Invalid shipment id for mark-paid";
            return false;
        }

        payload = new
        {
            query = _centraQueryCatalog.GetUpdateShipmentMarkPaidQuery(),
            variables = new { id = numericId },
            operationName = "updateShipmentMarkPaid"
        };
        return true;
    }

    internal bool TryBuildGoodToGoPayload(string shipmentId, out object? payload, out string? errorMessage)
    {
        payload = null;
        errorMessage = null;

        if (!int.TryParse(shipmentId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId))
        {
            errorMessage = "Invalid shipment id for good-to-go";
            return false;
        }

        payload = new
        {
            query = _centraQueryCatalog.GetUpdateShipmentGoodToGoQuery(),
            variables = new { id = numericId },
            operationName = "updateShipmentGoodToGo"
        };
        return true;
    }
}

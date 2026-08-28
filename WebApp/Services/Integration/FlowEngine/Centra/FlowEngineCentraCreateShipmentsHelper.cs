using System.Globalization;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

internal static class FlowEngineCentraCreateShipmentsHelper
{
    public static ShipmentLinePreparationResult PrepareLines(RawOrderData order)
    {
        var shipmentLines = new List<FlowEngineShipmentLineInput>();
        var cancellationLines = new List<FlowEngineShipmentLineInput>();

        foreach (var line in order.Lines)
        {
            var lineId = line.Id?.Trim();
            if (string.IsNullOrWhiteSpace(lineId) || line.Quantity <= 0)
                continue;

            var remaining = line.Quantity - line.ShippedQuantity;
            if (remaining <= 0)
                continue;

            var allocated = line.Allocations.Sum(allocation => Math.Max(0, allocation.Quantity));
            if (allocated >= remaining)
            {
                shipmentLines.Add(new FlowEngineShipmentLineInput { OrderLineId = lineId, Quantity = remaining });
                continue;
            }

            if (allocated > 0)
            {
                shipmentLines.Add(new FlowEngineShipmentLineInput { OrderLineId = lineId, Quantity = allocated });
                cancellationLines.Add(new FlowEngineShipmentLineInput { OrderLineId = lineId, Quantity = remaining - allocated });
                continue;
            }

            cancellationLines.Add(new FlowEngineShipmentLineInput { OrderLineId = lineId, Quantity = remaining });
        }

        return new ShipmentLinePreparationResult(shipmentLines, cancellationLines);
    }

    public static BatchShipmentPreflightClassification ClassifyBatchShipmentPreflight(ShipmentOrderContext order)
    {
        if (ShouldSkipAlreadyComplete(order.Status, order.ShipmentFlags))
            return BatchShipmentPreflightClassification.SkipAlreadyComplete;

        var normalizedStatus = NormalizeStatus(order.Status);
        if (normalizedStatus != "CONFIRMED" && normalizedStatus != "PROCESSING")
            return BatchShipmentPreflightClassification.SkipBatchExpectedConfirmed;

        return BatchShipmentPreflightClassification.ProceedToJeeves;
    }

    public static bool ShouldSkipAlreadyComplete(string? status, IReadOnlyList<ShipmentEligibilityFlags> shipments)
        => NormalizeStatus(status) == "SHIPPED" &&
           shipments.Count > 0 &&
           shipments.All(item => item.IsPaid && item.IsGoodToGo && item.IsCaptured);

    public static string NormalizeStatus(string? status)
        => status?.Trim().ToUpperInvariant() ?? string.Empty;

    public static string BatchExpectedConfirmedSkipMessage(string? actualStatus)
    {
        var normalizedStatus = NormalizeStatus(actualStatus);
        var raw = actualStatus?.Trim() ?? string.Empty;
        var displayedRaw = string.IsNullOrWhiteSpace(raw) ? "<empty>" : raw;
        var displayedNormalized = string.IsNullOrWhiteSpace(normalizedStatus) ? "<empty>" : normalizedStatus;
        return $"Skipped: batch expects CONFIRMED or PROCESSING (actual: {displayedRaw}; normalized: {displayedNormalized})";
    }

    public static string BatchAlreadyCompleteSkipMessage(string? actualStatus)
    {
        var normalizedStatus = NormalizeStatus(actualStatus);
        var raw = actualStatus?.Trim() ?? string.Empty;
        var displayedRaw = string.IsNullOrWhiteSpace(raw) ? "<empty>" : raw;
        var displayedNormalized = string.IsNullOrWhiteSpace(normalizedStatus) ? "<empty>" : normalizedStatus;
        return $"Skipped: already complete (actual: {displayedRaw}; normalized: {displayedNormalized}; shipment flags all true)";
    }

    public static string? CancellationSummaryMessage(string orderId, IReadOnlyList<FlowEngineShipmentLineInput> lines, bool dryRun)
    {
        if (lines.Count == 0)
            return null;

        var totalQuantity = lines.Sum(line => Math.Max(0, line.Quantity));
        var verb = dryRun ? "would cancel" : "canceling";
        return $"Order {orderId}: {verb} {totalQuantity} item(s) across {lines.Count} line(s).";
    }

    public static bool ShouldHardStopAfterCancellationFailure(int storeId)
        => storeId == 2;

    public static IEnumerable<string> FilterWarnings(IEnumerable<string> warnings, int storeId)
    {
        if (storeId != 2 && storeId != 4)
            return warnings.Where(warning => !string.IsNullOrWhiteSpace(warning));

        return warnings.Where(warning =>
        {
            if (string.IsNullOrWhiteSpace(warning))
                return false;
            var normalized = warning.ToLowerInvariant();
            return !normalized.Contains("paymenttype", StringComparison.Ordinal) &&
                   !normalized.Contains("capture", StringComparison.Ordinal) &&
                   !normalized.Contains("authorized", StringComparison.Ordinal);
        });
    }

    public static bool IsAlreadyShippedMessage(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();
        return normalized.Contains("already fully shipped", StringComparison.Ordinal) ||
               normalized.Contains("already shipped", StringComparison.Ordinal) ||
               normalized.Contains("is \"shipped\"", StringComparison.Ordinal) ||
               normalized.Contains("is shipped", StringComparison.Ordinal);
    }

    public static bool IsSingleRecoverableAlreadyCapturedError(IReadOnlyList<string> userErrors)
        => userErrors.Count == 1 && userErrors[0].Contains("already captured", StringComparison.OrdinalIgnoreCase);

    public static ShipmentState MapShipmentState(ShipmentStateNode node)
        => new(
            node.Id?.Value ?? string.Empty,
            node.IsCaptured ?? false,
            node.IsShipped ?? false,
            node.IsPaid ?? false,
            node.IsGoodToGo ?? false);

    public static ExistingShipmentInfo MapExistingShipment(ShipmentStateNode node)
        => new(
            node.Id?.Value ?? string.Empty,
            node.Number?.Value,
            node.IsCaptured ?? false,
            node.IsShipped ?? false,
            node.IsPaid ?? false,
            node.IsGoodToGo ?? false);

    public static ShipmentState MapExistingToState(ExistingShipmentInfo info)
        => new(info.ShipmentId, info.IsCaptured, info.IsShipped, info.IsPaid, info.IsGoodToGo);

    public static FlowEngineCreateShipmentResultRow FailedShipmentResult(
        string orderId,
        string orderNumber,
        int? storeId,
        string message,
        IReadOnlyList<FlowEngineShipmentLineInput> lines,
        IEnumerable<string> warnings)
    {
        return new FlowEngineCreateShipmentResultRow
        {
            OrderId = orderId,
            OrderNumber = orderNumber,
            Success = false,
            StoreId = storeId,
            ErrorMessage = message,
            Warnings = warnings.Where(warning => !string.IsNullOrWhiteSpace(warning)).ToList(),
            OrderLines = lines.ToList()
        };
    }

    public static IEnumerable<object> BuildShipmentLines(IReadOnlyList<FlowEngineShipmentLineInput> lines)
        => lines
            .Where(line => !string.IsNullOrWhiteSpace(line.OrderLineId))
            .Select(line =>
            {
                var trimmedId = line.OrderLineId.Trim();
                var orderLine = int.TryParse(trimmedId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId)
                    ? new Dictionary<string, object> { ["id"] = numericId }
                    : new Dictionary<string, object> { ["id"] = trimmedId };

                return new Dictionary<string, object>
                {
                    ["orderLine"] = orderLine,
                    ["quantity"] = line.Quantity
                };
            });
}

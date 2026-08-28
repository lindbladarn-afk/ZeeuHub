using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

internal sealed record ShipmentProcessOutcome(FlowEngineCreateShipmentResultRow Result, ShipmentProcessState State);

internal sealed record ShipmentMutationOutcome(
    bool Success,
    ShipmentState? Shipment,
    List<string> Warnings,
    string? ErrorMessage,
    bool AlreadyShipped,
    CaptureOperationKind CaptureKind = CaptureOperationKind.None);

internal sealed record ShipmentLinePreparationResult(
    List<FlowEngineShipmentLineInput> LinesForShipment,
    List<FlowEngineShipmentLineInput> LinesForCancellation);

internal sealed record ExistingShipmentsLookupResult(List<ExistingShipmentInfo> Shipments, string? ErrorMessage)
{
    public bool Success => string.IsNullOrWhiteSpace(ErrorMessage);
}

internal sealed record ExistingShipmentInfo(
    string ShipmentId,
    string? ShipmentNumber,
    bool IsCaptured,
    bool IsShipped,
    bool IsPaid,
    bool IsGoodToGo);

internal sealed record ShipmentState(
    string Id,
    bool IsCaptured,
    bool IsShipped,
    bool IsPaid,
    bool IsGoodToGo);

internal sealed record ShipmentOrderContext(
    string OrderId,
    string OrderNumber,
    string? Status,
    string? CreatedAt,
    int StoreId,
    List<ShipmentEligibilityFlags> ShipmentFlags,
    RawOrderData RawOrder);

internal sealed record ShipmentEligibilityFlags(
    bool IsPaid,
    bool IsGoodToGo,
    bool IsCaptured);

internal sealed record RawOrderData(
    string? Id,
    int Number,
    List<RawOrderLine> Lines);

internal sealed record RawOrderLine(
    string? Id,
    int Quantity,
    int ShippedQuantity,
    List<RawAllocation> Allocations);

internal sealed record RawAllocation(int Quantity);

internal sealed record JeevesOrderCheckResult
{
    public JeevesCheckStatus Status { get; init; }
    public int JeevesOrderStatus { get; init; }
    public int? OrderNumber { get; init; }
    public string? StatusName { get; init; }
    public string? TrackingUrl { get; init; }
}

internal enum JeevesCheckStatus
{
    Found,
    NotFound,
    Error
}

internal enum BatchShipmentPreflightClassification
{
    SkipAlreadyComplete,
    SkipBatchExpectedConfirmed,
    ProceedToJeeves
}

internal enum ShipmentProcessState
{
    Success,
    Skipped,
    Failed
}

internal enum CaptureOperationKind
{
    None,
    CapturedNow,
    AlreadyCaptured,
    Failed
}

internal sealed record CancellationAttempt(string Label, string Query, string OperationName);

internal sealed class ShipmentOrdersEnvelope
{
    public ShipmentOrdersData? Data { get; set; }
}

internal sealed class ShipmentOrderByIdEnvelope
{
    public ShipmentOrderByIdData? Data { get; set; }
}

internal sealed class ShipmentOrderByIdData
{
    public ShipmentOrderNode? Order { get; set; }
}

internal sealed class ShipmentOrdersData
{
    public List<ShipmentOrderNode>? Orders { get; set; }
}

internal sealed class ShipmentOrderNode
{
    public string Id { get; set; } = string.Empty;

    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Number { get; set; }

    public string? Status { get; set; }
    public string? CreatedAt { get; set; }
    public StoreNode? Store { get; set; }
    public List<ShipmentFlagNode>? Shipments { get; set; }
    public List<ShipmentLineNode>? Lines { get; set; }
}

internal sealed class StoreNode
{
    public int Id { get; set; }
}

internal sealed class ShipmentFlagNode
{
    public bool? IsPaid { get; set; }
    public bool? IsGoodToGo { get; set; }
    public bool? IsCaptured { get; set; }
}

internal sealed class ShipmentLineNode
{
    public FlexibleString? Id { get; set; }
    public LossyInt? Quantity { get; set; }
    public LossyInt? ShippedQuantity { get; set; }
    public List<ShipmentAllocationNode>? Allocations { get; set; }
}

internal sealed class ShipmentAllocationNode
{
    public LossyInt? Quantity { get; set; }
}

internal sealed class LossyInt
{
    public int Value { get; set; }
}

internal sealed class ShipmentMutationEnvelope
{
    public ShipmentMutationData? Data { get; set; }
}

internal sealed class ShipmentMutationData
{
    public ShipmentMutationPayload? CreateShipment { get; set; }
    public ShipmentMutationPayload? CaptureShipment { get; set; }
    public ShipmentMutationPayload? CompleteShipment { get; set; }
    public ShipmentMutationPayload? UpdateShipment { get; set; }

    public ShipmentMutationPayload? GetPayload(string fieldName)
        => fieldName switch
        {
            "createShipment" => CreateShipment,
            "captureShipment" => CaptureShipment,
            "completeShipment" => CompleteShipment,
            "updateShipment" => UpdateShipment,
            _ => null
        };
}

internal sealed class ShipmentMutationPayload
{
    public List<ShipmentUserMessage> UserErrors { get; set; } = new();
    public List<ShipmentUserMessage> UserWarnings { get; set; } = new();
    public ShipmentStateNode? Shipment { get; set; }
}

internal sealed class ShipmentUserMessage
{
    public string Message { get; set; } = string.Empty;
}

internal sealed class ShipmentStateNode
{
    public FlexibleString? Id { get; set; }
    public FlexibleString? Number { get; set; }
    public bool? IsCaptured { get; set; }
    public bool? IsShipped { get; set; }
    public bool? IsPaid { get; set; }
    public bool? IsGoodToGo { get; set; }
}

internal sealed class ExistingShipmentsEnvelope
{
    public ExistingShipmentsData? Data { get; set; }
}

internal sealed class ExistingShipmentsData
{
    public ExistingShipmentsOrder? Order { get; set; }
}

internal sealed class ExistingShipmentsOrder
{
    public List<ShipmentStateNode>? Shipments { get; set; }
}

internal sealed class CancelOrderLinesEnvelope
{
    public CancelOrderLinesData? Data { get; set; }
}

internal sealed class CancelOrderLinesData
{
    public CancelOrderLinesPayload? UpdateWholesaleOrder { get; set; }
    public CancelOrderLinesPayload? UpdateDirectToConsumerOrder { get; set; }
}

internal sealed class CancelOrderLinesPayload
{
    public List<ShipmentUserMessage> UserErrors { get; set; } = new();
}

internal sealed class FlexibleString
{
    public string Value { get; set; } = string.Empty;
}

internal sealed class FlexibleStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var intValue)
                ? intValue.ToString(CultureInfo.InvariantCulture)
                : reader.GetDecimal().ToString(CultureInfo.InvariantCulture),
            _ => null
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value);
    }
}

internal static class FlowEngineCentraCreateShipmentsMapper
{
    public static ShipmentOrderContext MapShipmentOrderNode(ShipmentOrderNode node)
    {
        var lineModels = (node.Lines ?? new List<ShipmentLineNode>())
            .Select(line => new RawOrderLine(
                line.Id?.Value,
                line.Quantity?.Value ?? 0,
                line.ShippedQuantity?.Value ?? 0,
                (line.Allocations ?? new List<ShipmentAllocationNode>())
                    .Select(allocation => new RawAllocation(allocation.Quantity?.Value ?? 0))
                    .ToList()))
            .ToList();

        return new ShipmentOrderContext(
            node.Id,
            node.Number ?? string.Empty,
            node.Status,
            node.CreatedAt,
            node.Store?.Id ?? 0,
            (node.Shipments ?? new List<ShipmentFlagNode>())
                .Select(shipment => new ShipmentEligibilityFlags(
                    shipment.IsPaid ?? false,
                    shipment.IsGoodToGo ?? false,
                    shipment.IsCaptured ?? false))
                .ToList(),
            new RawOrderData(
                node.Id,
                int.TryParse(node.Number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numberValue) ? numberValue : 0,
                lineModels));
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraShipmentMutationResultParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    internal ShipmentMutationOutcome Parse(string body, string fieldName, int storeId, bool captureAware)
    {
        if (TryGetGraphQlErrorMessage(body, out var graphQlError))
            return new ShipmentMutationOutcome(false, null, new List<string>(), graphQlError, false);

        var parsed = JsonSerializer.Deserialize<ShipmentMutationEnvelope>(body, JsonOptions);
        var payloadNode = parsed?.Data?.GetPayload(fieldName);
        if (payloadNode is null)
            return new ShipmentMutationOutcome(false, null, new List<string>(), "Mutation succeeded without shipment payload", false);

        var warnings = FlowEngineCentraCreateShipmentsHelper.FilterWarnings(payloadNode.UserWarnings.Select(item => item.Message), storeId).ToList();
        var userErrors = payloadNode.UserErrors.Select(item => item.Message).Where(message => !string.IsNullOrWhiteSpace(message)).ToList();
        if (userErrors.Count > 0)
        {
            var alreadyShipped = userErrors.Any(FlowEngineCentraCreateShipmentsHelper.IsAlreadyShippedMessage);
            if (captureAware && FlowEngineCentraCreateShipmentsHelper.IsSingleRecoverableAlreadyCapturedError(userErrors))
                return new ShipmentMutationOutcome(false, null, warnings, string.Join(" | ", userErrors), false, CaptureOperationKind.AlreadyCaptured);

            return new ShipmentMutationOutcome(false, null, warnings, string.Join(" | ", userErrors), alreadyShipped, captureAware ? CaptureOperationKind.Failed : CaptureOperationKind.None);
        }

        if (payloadNode.Shipment is null)
            return new ShipmentMutationOutcome(false, null, warnings, "Mutation succeeded without shipment payload", false, captureAware ? CaptureOperationKind.Failed : CaptureOperationKind.None);

        return new ShipmentMutationOutcome(true, FlowEngineCentraCreateShipmentsHelper.MapShipmentState(payloadNode.Shipment), warnings, null, false, captureAware ? CaptureOperationKind.CapturedNow : CaptureOperationKind.None);
    }

    internal bool TryGetGraphQlError(string body, out string errorMessage)
        => TryGetGraphQlErrorMessage(body, out errorMessage);

    internal IReadOnlyList<string> ParseCancellationUserErrors(string body)
    {
        var parsed = JsonSerializer.Deserialize<CancelOrderLinesEnvelope>(body, JsonOptions);
        var userErrors = parsed?.Data?.UpdateDirectToConsumerOrder?.UserErrors
                         ?? parsed?.Data?.UpdateWholesaleOrder?.UserErrors
                         ?? new List<ShipmentUserMessage>();
        return userErrors
            .Select(item => item.Message)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToList();
    }

    private static bool TryGetGraphQlErrorMessage(string body, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(body))
            return false;

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array)
                return false;

            var messages = errors.EnumerateArray()
                .Select(item => item.TryGetProperty("message", out var message) ? message.GetString() : null)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToList();

            if (messages.Count == 0)
                return false;

            errorMessage = string.Join(" | ", messages!);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

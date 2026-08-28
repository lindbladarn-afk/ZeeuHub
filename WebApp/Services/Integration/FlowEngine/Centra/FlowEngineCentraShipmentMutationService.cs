using System.Globalization;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraShipmentMutationService
{
    private readonly IFlowEngineCentraGraphQlClient _centraGraphQlClient;
    private readonly FlowEngineCentraShipmentLookupService _shipmentLookupService;
    private readonly FlowEngineCentraShipmentMutationPayloadFactory _payloadFactory;
    private readonly FlowEngineCentraShipmentMutationResultParser _resultParser;

    public FlowEngineCentraShipmentMutationService(
        IFlowEngineCentraGraphQlClient centraGraphQlClient,
        FlowEngineCentraShipmentLookupService shipmentLookupService,
        FlowEngineCentraShipmentMutationPayloadFactory payloadFactory,
        FlowEngineCentraShipmentMutationResultParser resultParser)
    {
        _centraGraphQlClient = centraGraphQlClient;
        _shipmentLookupService = shipmentLookupService;
        _payloadFactory = payloadFactory;
        _resultParser = resultParser;
    }

    internal async Task<(bool Success, string? ErrorMessage)> CancelUnallocatedLinesAsync(
        ShipmentOrderContext order,
        IReadOnlyList<FlowEngineShipmentLineInput> lines,
        IntegrationSourceConfig centraConfig,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
            return (true, null);

        if (!int.TryParse(order.OrderNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var orderNumber) || orderNumber <= 0)
            return (false, "Could not cancel unallocated lines: order number is missing/non-numeric");

        var attempts = _payloadFactory.BuildCancellationAttempts(order.StoreId, lines);
        var attemptErrors = new List<string>();

        foreach (var attempt in attempts)
        {
            var payload = new
            {
                query = attempt.Query,
                variables = new
                {
                    orderNumber,
                    cancellationComment = "Removed due to insufficient quantities"
                },
                operationName = attempt.OperationName
            };

            string body;
            try
            {
                body = await _centraGraphQlClient.PostAsync(centraConfig, payload, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                attemptErrors.Add($"{attempt.Label}: {ex.Message}");
                continue;
            }

            if (_resultParser.TryGetGraphQlError(body, out var gqlError))
            {
                attemptErrors.Add($"{attempt.Label}: {gqlError}");
                continue;
            }

            var userErrors = _resultParser.ParseCancellationUserErrors(body);
            if (userErrors.Count == 0)
                return (true, null);

            attemptErrors.Add($"{attempt.Label}: {string.Join(" | ", userErrors)}");
        }

        return (false, string.Join(" || ", attemptErrors));
    }

    internal async Task<ShipmentMutationOutcome> RunDefaultWorkflowAsync(
        ShipmentOrderContext order,
        IReadOnlyList<FlowEngineShipmentLineInput> shipmentLines,
        IntegrationSourceConfig centraConfig,
        CancellationToken cancellationToken)
    {
        var created = await CreateShipmentMutationAsync(order.OrderId, shipmentLines, captureOverride: null, order.StoreId, centraConfig, cancellationToken);
        if (created.AlreadyShipped)
        {
            return new ShipmentMutationOutcome(true, created.Shipment, new List<string> { "Shipment already existed as shipped" }, null, true);
        }

        return created;
    }

    internal async Task<ShipmentMutationOutcome> RunStore1WorkflowAsync(
        ShipmentOrderContext order,
        IReadOnlyList<FlowEngineShipmentLineInput> shipmentLines,
        IntegrationSourceConfig centraConfig,
        CancellationToken cancellationToken)
    {
        var created = await CreateShipmentMutationAsync(order.OrderId, shipmentLines, true, order.StoreId, centraConfig, cancellationToken);
        if (created.AlreadyShipped)
            return await RecoverAlreadyShippedStore1Async(order.OrderId, centraConfig, cancellationToken);

        if (!created.Success || created.Shipment is null)
            return created;

        var shipment = created.Shipment;
        var warnings = new List<string>(created.Warnings);

        var goodToGo = await UpdateShipmentGoodToGoAsync(shipment.Id, order.StoreId, centraConfig, cancellationToken);
        warnings.AddRange(goodToGo.Warnings);
        if (!goodToGo.Success)
            return new ShipmentMutationOutcome(false, null, warnings, goodToGo.ErrorMessage ?? "Failed to set GoodToGo", false);
        if (goodToGo.Shipment is not null)
            shipment = goodToGo.Shipment;

        if (!shipment.IsCaptured)
        {
            var capture = await CaptureShipmentAsync(shipment.Id, order.StoreId, centraConfig, cancellationToken);
            switch (capture.CaptureKind)
            {
                case CaptureOperationKind.CapturedNow:
                    warnings.AddRange(capture.Warnings);
                    if (capture.Shipment is not null)
                    {
                        shipment = capture.Shipment;
                    }
                    break;
                case CaptureOperationKind.AlreadyCaptured:
                    warnings.AddRange(capture.Warnings);
                    warnings.Add($"Recovered capture step from idempotent error: {capture.ErrorMessage}");
                    break;
                case CaptureOperationKind.Failed:
                    warnings.AddRange(capture.Warnings);
                    warnings.Add($"Capture step failed; proceeding to complete attempt: {capture.ErrorMessage}");
                    break;
            }
        }

        var completed = await CompleteShipmentAsync(shipment.Id, true, order.StoreId, centraConfig, cancellationToken);
        warnings.AddRange(completed.Warnings);
        if (!completed.Success)
            return new ShipmentMutationOutcome(false, null, warnings, completed.ErrorMessage ?? "Failed to complete shipment", false);

        return new ShipmentMutationOutcome(true, completed.Shipment ?? shipment, warnings, null, false);
    }

    internal async Task<ShipmentMutationOutcome> RunStore24WorkflowAsync(
        ShipmentOrderContext order,
        IReadOnlyList<FlowEngineShipmentLineInput> shipmentLines,
        IntegrationSourceConfig centraConfig,
        CancellationToken cancellationToken)
    {
        var created = await CreateShipmentMutationAsync(order.OrderId, shipmentLines, null, order.StoreId, centraConfig, cancellationToken);
        if (created.AlreadyShipped)
            return await RecoverAlreadyShippedStore24Async(order.OrderId, order.StoreId, centraConfig, cancellationToken);

        if (!created.Success || created.Shipment is null)
            return created;

        var shipment = created.Shipment;
        var warnings = new List<string>(created.Warnings);

        var paid = await MarkShipmentPaidAsync(shipment.Id, order.StoreId, centraConfig, cancellationToken);
        warnings.AddRange(paid.Warnings);
        if (!paid.Success)
            return new ShipmentMutationOutcome(false, null, warnings, paid.ErrorMessage ?? "Failed to mark shipment as paid", false);
        if (paid.Shipment is not null)
            shipment = paid.Shipment;

        var goodToGo = await UpdateShipmentGoodToGoAsync(shipment.Id, order.StoreId, centraConfig, cancellationToken);
        warnings.AddRange(goodToGo.Warnings);
        if (!goodToGo.Success)
            return new ShipmentMutationOutcome(false, null, warnings, goodToGo.ErrorMessage ?? "Failed to set GoodToGo", false);
        if (goodToGo.Shipment is not null)
            shipment = goodToGo.Shipment;

        var completed = await CompleteShipmentAsync(shipment.Id, true, order.StoreId, centraConfig, cancellationToken);
        warnings.AddRange(completed.Warnings);
        if (!completed.Success)
            return new ShipmentMutationOutcome(false, null, warnings, completed.ErrorMessage ?? "Failed to complete shipment", false);

        return new ShipmentMutationOutcome(true, completed.Shipment ?? shipment, warnings, null, false);
    }

    private async Task<ShipmentMutationOutcome> RecoverAlreadyShippedStore1Async(
        string orderId,
        IntegrationSourceConfig centraConfig,
        CancellationToken cancellationToken)
    {
        var lookup = await _shipmentLookupService.GetExistingShipmentsAsync(orderId, centraConfig, cancellationToken);
        if (!lookup.Success)
            return new ShipmentMutationOutcome(false, null, new List<string>(), lookup.ErrorMessage, true);
        if (lookup.Shipments.Count == 0)
            return new ShipmentMutationOutcome(false, null, new List<string>(), "Order reports already shipped, but no existing shipments were found", true);

        var target = lookup.Shipments.FirstOrDefault(item => !item.IsShipped || !item.IsCaptured || !item.IsGoodToGo);
        if (target is null)
            return new ShipmentMutationOutcome(true, FlowEngineCentraCreateShipmentsHelper.MapExistingToState(lookup.Shipments.First()), new List<string> { "Shipment was already fully processed" }, null, true);

        var warnings = new List<string> { "Recovered from already-shipped response using existing shipment" };
        var state = FlowEngineCentraCreateShipmentsHelper.MapExistingToState(target);

        if (!target.IsCaptured)
        {
            var capture = await CaptureShipmentAsync(target.ShipmentId, 1, centraConfig, cancellationToken);
            warnings.AddRange(capture.Warnings);
            if (capture.CaptureKind == CaptureOperationKind.Failed)
            {
                warnings.Add($"Capture failed during already-shipped recovery; proceeding to complete attempt: {capture.ErrorMessage}");
            }
            else if (capture.Shipment is not null)
            {
                state = capture.Shipment;
            }
        }

        if (!target.IsGoodToGo)
        {
            var goodToGo = await UpdateShipmentGoodToGoAsync(target.ShipmentId, 1, centraConfig, cancellationToken);
            warnings.AddRange(goodToGo.Warnings);
            if (!goodToGo.Success)
                return new ShipmentMutationOutcome(false, null, warnings, goodToGo.ErrorMessage ?? "GoodToGo failed during already-shipped recovery", true);
            if (goodToGo.Shipment is not null)
                state = goodToGo.Shipment;
        }

        if (!target.IsShipped)
        {
            var completed = await CompleteShipmentAsync(target.ShipmentId, true, 1, centraConfig, cancellationToken);
            warnings.AddRange(completed.Warnings);
            if (!completed.Success)
                return new ShipmentMutationOutcome(false, null, warnings, completed.ErrorMessage ?? "Complete failed during already-shipped recovery", true);
            return new ShipmentMutationOutcome(true, completed.Shipment ?? state, warnings, null, true);
        }

        return new ShipmentMutationOutcome(true, state, warnings, null, true);
    }

    private async Task<ShipmentMutationOutcome> RecoverAlreadyShippedStore24Async(
        string orderId,
        int storeId,
        IntegrationSourceConfig centraConfig,
        CancellationToken cancellationToken)
    {
        var lookup = await _shipmentLookupService.GetExistingShipmentsAsync(orderId, centraConfig, cancellationToken);
        if (!lookup.Success)
            return new ShipmentMutationOutcome(false, null, new List<string>(), lookup.ErrorMessage, true);
        if (lookup.Shipments.Count == 0)
            return new ShipmentMutationOutcome(false, null, new List<string>(), "Order reports already shipped, but no existing shipments were found", true);

        var target = lookup.Shipments.FirstOrDefault(item => !item.IsPaid) ?? lookup.Shipments.FirstOrDefault(item => !item.IsShipped);
        if (target is null)
            return new ShipmentMutationOutcome(true, FlowEngineCentraCreateShipmentsHelper.MapExistingToState(lookup.Shipments.First()), new List<string> { "Shipment was already fully processed" }, null, true);

        var warnings = new List<string> { "Recovered from already-shipped response using existing shipment" };
        var state = FlowEngineCentraCreateShipmentsHelper.MapExistingToState(target);

        if (!target.IsPaid)
        {
            var paid = await MarkShipmentPaidAsync(target.ShipmentId, storeId, centraConfig, cancellationToken);
            warnings.AddRange(paid.Warnings);
            if (!paid.Success)
                return new ShipmentMutationOutcome(false, null, warnings, paid.ErrorMessage ?? "Mark-paid failed during already-shipped recovery", true);
            if (paid.Shipment is not null)
                state = paid.Shipment;
        }

        if (!target.IsGoodToGo)
        {
            var goodToGo = await UpdateShipmentGoodToGoAsync(target.ShipmentId, storeId, centraConfig, cancellationToken);
            warnings.AddRange(goodToGo.Warnings);
            if (!goodToGo.Success)
                return new ShipmentMutationOutcome(false, null, warnings, goodToGo.ErrorMessage ?? "GoodToGo failed during already-shipped recovery", true);
            if (goodToGo.Shipment is not null)
                state = goodToGo.Shipment;
        }

        if (!target.IsShipped)
        {
            var completed = await CompleteShipmentAsync(target.ShipmentId, true, storeId, centraConfig, cancellationToken);
            warnings.AddRange(completed.Warnings);
            if (!completed.Success)
                return new ShipmentMutationOutcome(false, null, warnings, completed.ErrorMessage ?? "Complete failed during already-shipped recovery", true);
            return new ShipmentMutationOutcome(true, completed.Shipment ?? state, warnings, null, true);
        }

        return new ShipmentMutationOutcome(true, state, warnings, null, true);
    }

    private async Task<ShipmentMutationOutcome> CreateShipmentMutationAsync(
        string orderId,
        IReadOnlyList<FlowEngineShipmentLineInput> lines,
        bool? captureOverride,
        int storeId,
        IntegrationSourceConfig centraConfig,
        CancellationToken cancellationToken)
    {
        var payload = _payloadFactory.BuildCreateShipmentPayload(orderId, lines, captureOverride);

        return await ExecuteShipmentMutationAsync(payload, "createShipment", storeId, centraConfig, cancellationToken);
    }

    private Task<ShipmentMutationOutcome> CaptureShipmentAsync(
        string shipmentId,
        int storeId,
        IntegrationSourceConfig centraConfig,
        CancellationToken cancellationToken)
    {
        if (!_payloadFactory.TryBuildCapturePayload(shipmentId, out var payload, out var errorMessage))
            return Task.FromResult(new ShipmentMutationOutcome(false, null, new List<string>(), errorMessage ?? "Invalid shipment id for capture", false, CaptureOperationKind.Failed));

        return ExecuteShipmentMutationAsync(payload, "captureShipment", storeId, centraConfig, cancellationToken, captureAware: true);
    }

    private Task<ShipmentMutationOutcome> CompleteShipmentAsync(
        string shipmentId,
        bool sendEmail,
        int storeId,
        IntegrationSourceConfig centraConfig,
        CancellationToken cancellationToken)
    {
        if (!_payloadFactory.TryBuildCompletePayload(shipmentId, sendEmail, out var payload, out var errorMessage))
            return Task.FromResult(new ShipmentMutationOutcome(false, null, new List<string>(), errorMessage ?? "Invalid shipment id for complete", false));

        return ExecuteShipmentMutationAsync(payload, "completeShipment", storeId, centraConfig, cancellationToken);
    }

    private Task<ShipmentMutationOutcome> MarkShipmentPaidAsync(
        string shipmentId,
        int storeId,
        IntegrationSourceConfig centraConfig,
        CancellationToken cancellationToken)
    {
        if (!_payloadFactory.TryBuildMarkPaidPayload(shipmentId, out var payload, out var errorMessage))
            return Task.FromResult(new ShipmentMutationOutcome(false, null, new List<string>(), errorMessage ?? "Invalid shipment id for mark-paid", false));

        return ExecuteShipmentMutationAsync(payload, "updateShipment", storeId, centraConfig, cancellationToken);
    }

    private Task<ShipmentMutationOutcome> UpdateShipmentGoodToGoAsync(
        string shipmentId,
        int storeId,
        IntegrationSourceConfig centraConfig,
        CancellationToken cancellationToken)
    {
        if (!_payloadFactory.TryBuildGoodToGoPayload(shipmentId, out var payload, out var errorMessage))
            return Task.FromResult(new ShipmentMutationOutcome(false, null, new List<string>(), errorMessage ?? "Invalid shipment id for good-to-go", false));

        return ExecuteShipmentMutationAsync(payload, "updateShipment", storeId, centraConfig, cancellationToken);
    }

    private async Task<ShipmentMutationOutcome> ExecuteShipmentMutationAsync(
        object payload,
        string fieldName,
        int storeId,
        IntegrationSourceConfig centraConfig,
        CancellationToken cancellationToken,
        bool captureAware = false)
    {
        string body;
        try
        {
            body = await _centraGraphQlClient.PostAsync(centraConfig, payload, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return new ShipmentMutationOutcome(false, null, new List<string>(), ex.Message, false);
        }

        return _resultParser.Parse(body, fieldName, storeId, captureAware);
    }
}

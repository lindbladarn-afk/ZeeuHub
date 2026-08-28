using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraShipmentLookupService
{
    private const int DefaultPageSize = 50;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly IFlowEngineCentraGraphQlClient _centraGraphQlClient;
    private readonly IFlowEngineCentraQueryCatalog _centraQueryCatalog;

    public FlowEngineCentraShipmentLookupService(
        IFlowEngineCentraGraphQlClient centraGraphQlClient,
        IFlowEngineCentraQueryCatalog centraQueryCatalog)
    {
        _centraGraphQlClient = centraGraphQlClient;
        _centraQueryCatalog = centraQueryCatalog;
    }

    internal async Task<List<ShipmentOrderContext>> FetchShipmentOrdersByDateAsync(
        IntegrationSourceConfig centraConfig,
        DateTime dateUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            return await FetchShipmentOrdersByDateInternalAsync(centraConfig, dateUtc, includeShippedQuantity: true, cancellationToken);
        }
        catch (Exception ex) when (ShouldRetryShipmentFetchWithoutShippedQuantity(ex))
        {
            return await FetchShipmentOrdersByDateInternalAsync(centraConfig, dateUtc, includeShippedQuantity: false, cancellationToken);
        }
    }

    internal async Task<ShipmentOrderContext?> FetchShipmentOrderByIdAsync(
        IntegrationSourceConfig centraConfig,
        string orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await FetchShipmentOrderByIdInternalAsync(centraConfig, orderId, includeShippedQuantity: true, cancellationToken);
        }
        catch (Exception ex) when (ShouldRetryShipmentFetchWithoutShippedQuantity(ex))
        {
            return await FetchShipmentOrderByIdInternalAsync(centraConfig, orderId, includeShippedQuantity: false, cancellationToken);
        }
    }

    internal async Task<List<ShipmentOrderContext>> FetchShipmentOrdersByStatusesAsync(
        IntegrationSourceConfig centraConfig,
        IReadOnlyList<string> statuses,
        CancellationToken cancellationToken)
    {
        try
        {
            return await FetchShipmentOrdersByStatusesInternalAsync(centraConfig, statuses, includeShippedQuantity: true, cancellationToken);
        }
        catch (Exception ex) when (ShouldRetryShipmentFetchWithoutShippedQuantity(ex))
        {
            return await FetchShipmentOrdersByStatusesInternalAsync(centraConfig, statuses, includeShippedQuantity: false, cancellationToken);
        }
    }

    internal async Task<ExistingShipmentsLookupResult> GetExistingShipmentsAsync(
        string orderId,
        IntegrationSourceConfig centraConfig,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            query = _centraQueryCatalog.GetOrderShipmentsQuery(),
            variables = new { id = orderId },
            operationName = "getOrderShipments"
        };

        string body;
        try
        {
            body = await _centraGraphQlClient.PostAsync(centraConfig, payload, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return new ExistingShipmentsLookupResult(new List<ExistingShipmentInfo>(), ex.Message);
        }

        if (TryGetGraphQlErrorMessage(body, out var graphQlError))
            return new ExistingShipmentsLookupResult(new List<ExistingShipmentInfo>(), graphQlError);

        var parsed = JsonSerializer.Deserialize<ExistingShipmentsEnvelope>(body, JsonOptions);
        var shipments = parsed?.Data?.Order?.Shipments?.Select(FlowEngineCentraCreateShipmentsHelper.MapExistingShipment).ToList() ?? new List<ExistingShipmentInfo>();
        return new ExistingShipmentsLookupResult(shipments, null);
    }

    private async Task<List<ShipmentOrderContext>> FetchShipmentOrdersByDateInternalAsync(
        IntegrationSourceConfig centraConfig,
        DateTime dateUtc,
        bool includeShippedQuantity,
        CancellationToken cancellationToken)
    {
        var results = new List<ShipmentOrderContext>();
        var page = 1;
        while (true)
        {
            var payload = new
            {
                query = _centraQueryCatalog.GetShipmentOrdersByDateQuery(includeShippedQuantity),
                variables = new
                {
                    from = dateUtc.ToString("yyyy-MM-ddT00:00:00Z", CultureInfo.InvariantCulture),
                    to = dateUtc.AddDays(1).ToString("yyyy-MM-ddT00:00:00Z", CultureInfo.InvariantCulture),
                    limit = DefaultPageSize,
                    page
                },
                operationName = "ShipmentOrdersByDate"
            };

            var body = await _centraGraphQlClient.PostAsync(centraConfig, payload, cancellationToken);

            if (TryGetGraphQlErrorMessage(body, out var graphQlError))
                throw new InvalidOperationException(graphQlError);

            var parsed = JsonSerializer.Deserialize<ShipmentOrdersEnvelope>(body, JsonOptions);
            var nodes = parsed?.Data?.Orders ?? new List<ShipmentOrderNode>();
            if (nodes.Count == 0)
                break;

            results.AddRange(nodes.Select(FlowEngineCentraCreateShipmentsMapper.MapShipmentOrderNode));
            if (nodes.Count < DefaultPageSize)
                break;

            page++;
        }

        return results;
    }

    private async Task<ShipmentOrderContext?> FetchShipmentOrderByIdInternalAsync(
        IntegrationSourceConfig centraConfig,
        string orderId,
        bool includeShippedQuantity,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            query = _centraQueryCatalog.GetShipmentOrderByIdQuery(includeShippedQuantity),
            variables = new { id = orderId },
            operationName = "ShipmentOrderById"
        };

        var body = await _centraGraphQlClient.PostAsync(centraConfig, payload, cancellationToken);

        if (TryGetGraphQlErrorMessage(body, out var graphQlError))
            throw new InvalidOperationException(graphQlError);

        var parsed = JsonSerializer.Deserialize<ShipmentOrderByIdEnvelope>(body, JsonOptions);
        return parsed?.Data?.Order is null ? null : FlowEngineCentraCreateShipmentsMapper.MapShipmentOrderNode(parsed.Data.Order);
    }

    private async Task<List<ShipmentOrderContext>> FetchShipmentOrdersByStatusesInternalAsync(
        IntegrationSourceConfig centraConfig,
        IReadOnlyList<string> statuses,
        bool includeShippedQuantity,
        CancellationToken cancellationToken)
    {
        var results = new List<ShipmentOrderContext>();
        var page = 1;
        while (true)
        {
            var payload = new
            {
                query = _centraQueryCatalog.GetShipmentOrdersByStatusQuery(statuses, includeShippedQuantity),
                variables = new
                {
                    limit = DefaultPageSize,
                    page
                },
                operationName = "ShipmentOrdersByStatus"
            };

            var body = await _centraGraphQlClient.PostAsync(centraConfig, payload, cancellationToken);

            if (TryGetGraphQlErrorMessage(body, out var graphQlError))
                throw new InvalidOperationException(graphQlError);

            var parsed = JsonSerializer.Deserialize<ShipmentOrdersEnvelope>(body, JsonOptions);
            var nodes = parsed?.Data?.Orders ?? new List<ShipmentOrderNode>();
            if (nodes.Count == 0)
                break;

            results.AddRange(nodes.Select(FlowEngineCentraCreateShipmentsMapper.MapShipmentOrderNode));
            if (nodes.Count < DefaultPageSize)
                break;

            page++;
        }

        return results;
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

    private static bool ShouldRetryShipmentFetchWithoutShippedQuantity(Exception exception)
    {
        return exception.Message.Contains("shippedQuantity", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("400", StringComparison.OrdinalIgnoreCase);
    }
}

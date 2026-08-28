using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineShopifyFulfillmentService : IFlowEngineShopifyFulfillmentService
{
    private readonly IFlowEngineShopifyGraphQlClient _shopifyGraphQlClient;
    private readonly IFlowEngineShopifyQueryCatalog _shopifyQueryCatalog;

    public FlowEngineShopifyFulfillmentService(
        IFlowEngineShopifyGraphQlClient shopifyGraphQlClient,
        IFlowEngineShopifyQueryCatalog shopifyQueryCatalog)
    {
        _shopifyGraphQlClient = shopifyGraphQlClient;
        _shopifyQueryCatalog = shopifyQueryCatalog;
    }

    public async Task<List<string>> CollectActionableFulfillmentOrderIdsAsync(
        Uri endpointUrl,
        string accessToken,
        string orderGid,
        CancellationToken cancellationToken = default)
    {
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await _shopifyGraphQlClient.PostAsync<ShopifyFetchFulfillmentOrdersData>(
                endpointUrl,
                accessToken,
                _shopifyQueryCatalog.FetchFulfillmentOrdersQuery,
                new Dictionary<string, object?>
                {
                    ["id"] = orderGid,
                    ["after"] = cursor
                },
                "ShopifyFetchFulfillmentOrders",
                cancellationToken);

            var connection = response.Order?.FulfillmentOrders;
            var edges = connection?.Edges ?? new List<ShopifyFulfillmentOrderEdge>();
            foreach (var edge in edges)
            {
                var node = edge.Node;
                if (node is null || string.IsNullOrWhiteSpace(node.Id) || !IsActionableFulfillmentStatus(node.Status))
                    continue;

                if (seen.Add(node.Id))
                    ids.Add(node.Id);
            }

            if (connection?.PageInfo?.HasNextPage != true || string.IsNullOrWhiteSpace(connection.PageInfo.EndCursor))
                break;

            cursor = connection.PageInfo.EndCursor;
        }

        return ids;
    }

    public async Task<FlowEngineShopifyCreateFulfillmentResult> CreateFulfillmentAsync(
        Uri endpointUrl,
        string accessToken,
        string orderGid,
        IReadOnlyList<string> fulfillmentOrderIds,
        string? trackingUrl,
        string? trackingNumber,
        CancellationToken cancellationToken = default)
    {
        var fulfillmentInput = new Dictionary<string, object?>
        {
            ["lineItemsByFulfillmentOrder"] = fulfillmentOrderIds
                .Select(id => new Dictionary<string, object?> { ["fulfillmentOrderId"] = id })
                .ToList(),
            ["notifyCustomer"] = true
        };

        var trackingInfo = BuildTrackingInfo(trackingUrl, trackingNumber);
        if (trackingInfo.Count > 0)
            fulfillmentInput["trackingInfo"] = trackingInfo;

        var response = await _shopifyGraphQlClient.PostAsync<ShopifyFulfillmentCreateData>(
            endpointUrl,
            accessToken,
            _shopifyQueryCatalog.FulfillmentCreateMutation,
            new Dictionary<string, object?>
            {
                ["fulfillment"] = fulfillmentInput
            },
            "ShopifyFulfillmentCreate",
            cancellationToken);

        var payload = response.FulfillmentCreate;
        var userErrors = payload?.UserErrors?.Select(error => error.Message).Where(message => !string.IsNullOrWhiteSpace(message)).ToList()
                         ?? new List<string>();
        if (userErrors.Count > 0)
            return new FlowEngineShopifyCreateFulfillmentResult(false, null, $"Shopify fulfillmentCreate failed: {string.Join(" | ", userErrors)}");

        return new FlowEngineShopifyCreateFulfillmentResult(true, payload?.Fulfillment?.Id, null);
    }

    public async Task<FlowEngineShopifyCloseOrderResult> CloseOrderAsync(
        Uri endpointUrl,
        string accessToken,
        string orderGid,
        CancellationToken cancellationToken = default)
    {
        var response = await _shopifyGraphQlClient.PostAsync<ShopifyOrderCloseData>(
            endpointUrl,
            accessToken,
            _shopifyQueryCatalog.OrderCloseMutation,
            new Dictionary<string, object?>
            {
                ["input"] = new Dictionary<string, object?> { ["id"] = orderGid }
            },
            "ShopifyOrderClose",
            cancellationToken);

        var payload = response.OrderClose;
        var userErrors = payload?.UserErrors?.Select(error => error.Message).Where(message => !string.IsNullOrWhiteSpace(message)).ToList()
                         ?? new List<string>();
        if (userErrors.Count > 0)
            return new FlowEngineShopifyCloseOrderResult(false, false, $"Shopify orderClose failed: {string.Join(" | ", userErrors)}");

        return new FlowEngineShopifyCloseOrderResult(true, payload?.Order?.Closed == true, null);
    }

    public Task TryAddTagAsync(
        Uri endpointUrl,
        string accessToken,
        string orderGid,
        string tag,
        CancellationToken cancellationToken = default)
    {
        return _shopifyGraphQlClient.PostAsync<ShopifyTagsAddData>(
            endpointUrl,
            accessToken,
            _shopifyQueryCatalog.TagsAddMutation,
            new Dictionary<string, object?>
            {
                ["id"] = orderGid,
                ["tags"] = new[] { tag }
            },
            "ShopifyTagsAdd",
            cancellationToken);
    }

    private static bool IsActionableFulfillmentStatus(string? rawStatus)
    {
        var status = string.IsNullOrWhiteSpace(rawStatus) ? string.Empty : rawStatus.Trim().ToUpperInvariant();
        return status is not ("CLOSED" or "CANCELLED" or "INCOMPLETE");
    }

    private static Dictionary<string, object?> BuildTrackingInfo(string? trackingUrl, string? trackingNumber)
    {
        var info = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(trackingNumber))
            info["number"] = trackingNumber;
        if (!string.IsNullOrWhiteSpace(trackingUrl))
            info["url"] = trackingUrl;
        return info;
    }

    private sealed class ShopifyFetchFulfillmentOrdersData
    {
        public ShopifyOrderWithFulfillmentOrders? Order { get; set; }
    }

    private sealed class ShopifyOrderWithFulfillmentOrders
    {
        public string? Id { get; set; }
        public ShopifyFulfillmentOrdersConnection? FulfillmentOrders { get; set; }
    }

    private sealed class ShopifyFulfillmentOrdersConnection
    {
        public ShopifyPageInfo? PageInfo { get; set; }
        public List<ShopifyFulfillmentOrderEdge>? Edges { get; set; }
    }

    private sealed class ShopifyFulfillmentOrderEdge
    {
        public ShopifyFulfillmentOrderNode? Node { get; set; }
    }

    private sealed class ShopifyFulfillmentOrderNode
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
        public string? RequestStatus { get; set; }
    }

    private sealed class ShopifyPageInfo
    {
        public bool HasNextPage { get; set; }
        public string? EndCursor { get; set; }
    }

    private sealed class ShopifyFulfillmentCreateData
    {
        public ShopifyFulfillmentCreatePayload? FulfillmentCreate { get; set; }
    }

    private sealed class ShopifyFulfillmentCreatePayload
    {
        public ShopifyFulfillment? Fulfillment { get; set; }
        public List<ShopifyUserError>? UserErrors { get; set; }
    }

    private sealed class ShopifyFulfillment
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
    }

    private sealed class ShopifyOrderCloseData
    {
        public ShopifyOrderClosePayload? OrderClose { get; set; }
    }

    private sealed class ShopifyOrderClosePayload
    {
        public ShopifyClosedOrder? Order { get; set; }
        public List<ShopifyUserError>? UserErrors { get; set; }
    }

    private sealed class ShopifyClosedOrder
    {
        public string? Id { get; set; }
        public bool? Closed { get; set; }
    }

    private sealed class ShopifyTagsAddData
    {
        public ShopifyTagsAddPayload? TagsAdd { get; set; }
    }

    private sealed class ShopifyTagsAddPayload
    {
        public List<ShopifyUserError>? UserErrors { get; set; }
    }

    private sealed class ShopifyUserError
    {
        public List<string>? Field { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}

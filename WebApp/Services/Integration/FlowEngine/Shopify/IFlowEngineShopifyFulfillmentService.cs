using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineShopifyFulfillmentService
{
    Task<List<string>> CollectActionableFulfillmentOrderIdsAsync(
        Uri endpointUrl,
        string accessToken,
        string orderGid,
        CancellationToken cancellationToken = default);

    Task<FlowEngineShopifyCreateFulfillmentResult> CreateFulfillmentAsync(
        Uri endpointUrl,
        string accessToken,
        string orderGid,
        IReadOnlyList<string> fulfillmentOrderIds,
        string? trackingUrl,
        string? trackingNumber,
        CancellationToken cancellationToken = default);

    Task<FlowEngineShopifyCloseOrderResult> CloseOrderAsync(
        Uri endpointUrl,
        string accessToken,
        string orderGid,
        CancellationToken cancellationToken = default);

    Task TryAddTagAsync(
        Uri endpointUrl,
        string accessToken,
        string orderGid,
        string tag,
        CancellationToken cancellationToken = default);
}

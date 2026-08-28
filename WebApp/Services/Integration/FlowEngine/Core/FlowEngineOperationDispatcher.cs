using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineOperationDispatcher : IFlowEngineOperationDispatcher
{
    private readonly IFlowEngineConfigValidationService _configValidationService;
    private readonly IFlowEngineCentraReadService _centraReadService;
    private readonly IFlowEngineCentraCheckOrdersService _centraCheckOrdersService;
    private readonly IFlowEngineCentraCreateShipmentsService _centraCreateShipmentsService;
    private readonly IFlowEngineCentraSendOrdersService _centraSendOrdersService;
    private readonly IFlowEngineCentraSendReturnsService _centraSendReturnsService;
    private readonly IFlowEngineShopifyCompleteOrdersService _shopifyCompleteOrdersService;
    private readonly IFlowEngineShopifyReadService _shopifyReadService;
    private readonly IFlowEngineAkeneoExportService _akeneoExportService;
    private readonly IFlowEngineAkeneoSendToShopifyService _akeneoSendToShopifyService;
    private readonly IFlowEngineAkeneoSendToCentraService _akeneoSendToCentraService;
    private readonly IFlowEngineJeevesReadService _jeevesReadService;
    private readonly IFlowEngineJeevesImportOrderService _jeevesImportOrderService;

    public FlowEngineOperationDispatcher(
        IFlowEngineConfigValidationService configValidationService,
        IFlowEngineCentraReadService centraReadService,
        IFlowEngineCentraCheckOrdersService centraCheckOrdersService,
        IFlowEngineCentraCreateShipmentsService centraCreateShipmentsService,
        IFlowEngineCentraSendOrdersService centraSendOrdersService,
        IFlowEngineCentraSendReturnsService centraSendReturnsService,
        IFlowEngineShopifyCompleteOrdersService shopifyCompleteOrdersService,
        IFlowEngineShopifyReadService shopifyReadService,
        IFlowEngineAkeneoExportService akeneoExportService,
        IFlowEngineAkeneoSendToShopifyService akeneoSendToShopifyService,
        IFlowEngineAkeneoSendToCentraService akeneoSendToCentraService,
        IFlowEngineJeevesReadService jeevesReadService,
        IFlowEngineJeevesImportOrderService jeevesImportOrderService)
    {
        _configValidationService = configValidationService;
        _centraReadService = centraReadService;
        _centraCheckOrdersService = centraCheckOrdersService;
        _centraCreateShipmentsService = centraCreateShipmentsService;
        _centraSendOrdersService = centraSendOrdersService;
        _centraSendReturnsService = centraSendReturnsService;
        _shopifyCompleteOrdersService = shopifyCompleteOrdersService;
        _shopifyReadService = shopifyReadService;
        _akeneoExportService = akeneoExportService;
        _akeneoSendToShopifyService = akeneoSendToShopifyService;
        _akeneoSendToCentraService = akeneoSendToCentraService;
        _jeevesReadService = jeevesReadService;
        _jeevesImportOrderService = jeevesImportOrderService;
    }

    public async Task<FlowEngineOperationExecutionData> DispatchAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken = default)
    {
        return request.Operation switch
        {
            FlowEngineOperationType.ConfigValidate => await _configValidationService.ExecuteAsync(
                runtimeContext,
                cancellationToken),
            FlowEngineOperationType.CentraFetchOrder => await _centraReadService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.CentraFetchOrders => await _centraReadService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.CentraFetchReturn => await _centraReadService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.CentraFetchReturns => await _centraReadService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.CheckOrders => await _centraCheckOrdersService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.CreateShipments => await _centraCreateShipmentsService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.CreateShipment => await _centraCreateShipmentsService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.CreateShipmentsPending => await _centraCreateShipmentsService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.SendOrder => await _centraSendOrdersService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.SendOrders => await _centraSendOrdersService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.SendReturn => await _centraSendReturnsService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.SendReturns => await _centraSendReturnsService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.CompleteOrder => await _shopifyCompleteOrdersService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.AkeneoProducts => await _akeneoExportService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.AkeneoAllProducts => await _akeneoExportService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.AkeneoSendToShopify => await _akeneoSendToShopifyService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.AkeneoSendToCentra => await _akeneoSendToCentraService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.ShopifyScopesCheck => await _shopifyReadService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.ShopifyGetProducts => await _shopifyReadService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.ShopifyFetchOrder => await _shopifyReadService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.ShopifyFetchOrders => await _shopifyReadService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.ShopifyValidateOrder => await _shopifyReadService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.ShopifyValidateOrders => await _shopifyReadService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.ShopifyCheckOrders => await _shopifyReadService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.ShopifySendOrder => await _shopifyReadService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.ShopifySendOrders => await _shopifyReadService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.CompleteOrders => await _shopifyCompleteOrdersService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.CompleteOrdersPending => await _shopifyCompleteOrdersService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            FlowEngineOperationType.GetOrders => await _jeevesReadService.GetOrdersAsync(
                runtimeContext,
                request.Params.JeevesCompanyCode ?? runtimeContext.CompanyCode,
                request.Params.JeevesLookupField ?? string.Empty,
                request.Params.JeevesLookupValue ?? string.Empty,
                cancellationToken),
            FlowEngineOperationType.OrderExists => await _jeevesReadService.OrderExistsAsync(
                runtimeContext,
                request.Params.OrderId ?? string.Empty,
                cancellationToken),
            FlowEngineOperationType.GetCustomerAddresses => await _jeevesReadService.GetCustomerAddressesAsync(
                runtimeContext,
                request.Params.JeevesCustomerNumber ?? string.Empty,
                cancellationToken),
            FlowEngineOperationType.GetProduct => await _jeevesReadService.GetProductAsync(
                runtimeContext,
                request.Params.JeevesProductArticleNumber ?? string.Empty,
                cancellationToken),
            FlowEngineOperationType.GetArtStatus => await _jeevesReadService.GetArtStatusAsync(
                runtimeContext,
                request.Params.JeevesProductArticleNumbers,
                cancellationToken),
            FlowEngineOperationType.ImportOrder => await _jeevesImportOrderService.ExecuteAsync(
                runtimeContext,
                request,
                cancellationToken),
            _ => throw new InvalidOperationException($"Operationen {request.Operation} ar inte implementerad i portalens native FlowEngine an.")
        };
    }
}

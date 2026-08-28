using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineCentraQueryCatalog
{
    string GetFetchOrderQuery();
    string GetFetchOrdersByDateQuery();
    string GetFetchReturnQuery();
    string GetFetchReturnsByDateQuery();
    string GetSendOrdersByDateQuery();
    string GetSendOrderByIdQuery();
    string GetSendOrderByLookupQuery();
    string GetSendReturnsByDateQuery();
    string GetSendReturnByIdQuery();
    string GetShipmentOrdersByDateQuery(bool includeShippedQuantity);
    string GetShipmentOrderByIdQuery(bool includeShippedQuantity);
    string GetShipmentOrdersByStatusQuery(IReadOnlyList<string> statuses, bool includeShippedQuantity);
    string GetCreateShipmentQuery();
    string GetCreateShipmentWithCaptureQuery();
    string GetCaptureShipmentQuery();
    string GetCompleteShipmentQuery();
    string GetUpdateShipmentMarkPaidQuery();
    string GetUpdateShipmentGoodToGoQuery();
    string GetOrderShipmentsQuery();
    string GetCancelOrderLinesWholesaleQuery(IReadOnlyList<FlowEngineShipmentLineInput> lines, bool includeStockAction);
    string GetCancelOrderLinesDirectToConsumerQuery(IReadOnlyList<FlowEngineShipmentLineInput> lines, bool includeStockAction);
}

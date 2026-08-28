// Defines how Centra form input is translated into executable Flow Engine commands.
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineCentraCommandFactory
{
    FlowEngineExecuteJobRequest BuildCheckOrders(FlowEngineRunCheckOrdersInput input);
    FlowEngineExecuteJobRequest BuildFetchOrder(FlowEngineRunCentraFetchOrderInput input);
    FlowEngineExecuteJobRequest BuildFetchOrders(FlowEngineRunCentraFetchOrdersInput input);
    FlowEngineExecuteJobRequest BuildFetchReturn(FlowEngineRunCentraFetchReturnInput input);
    FlowEngineExecuteJobRequest BuildFetchReturns(FlowEngineRunCentraFetchReturnsInput input);
    FlowEngineExecuteJobRequest BuildCreateShipments(FlowEngineRunCreateShipmentsInput input);
    FlowEngineExecuteJobRequest BuildCreateShipmentsPending(FlowEngineRunCreateShipmentsPendingInput input);
    FlowEngineExecuteJobRequest BuildCreateShipment(FlowEngineRunCreateShipmentInput input);
    FlowEngineExecuteJobRequest BuildSendOrder(FlowEngineRunSendOrderInput input);
    FlowEngineExecuteJobRequest BuildSendOrders(FlowEngineRunSendOrdersInput input);
    FlowEngineExecuteJobRequest BuildSendReturn(FlowEngineRunSendReturnInput input);
    FlowEngineExecuteJobRequest BuildSendReturns(FlowEngineRunSendReturnsInput input);
}

using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineShopifyOrderMapper
{
    FlowEngineShopifyJeevesOrderPayload MapToJeevesOrder(FlowEngineShopifyOrderMappingInput input);
}

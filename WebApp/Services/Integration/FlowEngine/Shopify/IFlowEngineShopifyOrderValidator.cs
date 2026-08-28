using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineShopifyOrderValidator
{
    FlowEngineShopifyValidationDecision Validate(FlowEngineShopifyOrderValidationInput input);
}

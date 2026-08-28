namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineShopifyQueryCatalog
{
    string BuildGetProductsQuery(bool includeInventoryItem, bool includeMetafields);
    string ScopesCheckQuery { get; }
    string CurrentAccessScopesQuery { get; }
    string FetchOrderQuery { get; }
    string FetchOrdersByDateQuery { get; }
    string ValidateOrdersByDateQuery { get; }
    string TagsAddMutation { get; }
    string FetchFulfillmentOrdersQuery { get; }
    string FulfillmentCreateMutation { get; }
    string OrderCloseMutation { get; }
}

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineShopifyGraphQlClient
{
    Task<T> PostAsync<T>(
        Uri endpointUrl,
        string accessToken,
        string query,
        Dictionary<string, object?> variables,
        string operationName,
        CancellationToken cancellationToken = default)
        where T : class;
}

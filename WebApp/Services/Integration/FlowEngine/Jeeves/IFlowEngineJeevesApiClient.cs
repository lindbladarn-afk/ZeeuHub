using System.Net;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineJeevesApiClient
{
    IntegrationSourceConfig ResolveConfig(Guid companyId, string errorContext);
    string BuildRequestUri(string baseUrl, string relativePath, IReadOnlyDictionary<string, string?>? query);
    Task<FlowEngineJeevesApiResponse> SendAuthorizedAsync(
        Guid companyId,
        IntegrationSourceConfig config,
        HttpMethod method,
        string uri,
        string? jsonBody,
        CancellationToken cancellationToken);
}

public sealed record FlowEngineJeevesApiResponse(HttpStatusCode StatusCode, string Body)
{
    public bool IsSuccessStatusCode => (int)StatusCode is >= 200 and <= 299;
}

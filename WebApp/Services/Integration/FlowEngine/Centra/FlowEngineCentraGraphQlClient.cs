using System.Net.Http.Json;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraGraphQlClient : IFlowEngineCentraGraphQlClient
{
    private readonly IFlowEngineCentraConnectionService _centraConnectionService;

    public FlowEngineCentraGraphQlClient(IFlowEngineCentraConnectionService centraConnectionService)
    {
        _centraConnectionService = centraConnectionService;
    }

    public async Task<string> PostAsync(
        IntegrationSourceConfig centraConfig,
        object payload,
        CancellationToken cancellationToken = default)
    {
        using var client = _centraConnectionService.CreateClient(centraConfig);
        var requestUri = _centraConnectionService.GetRequestUri(centraConfig);
        using var response = await client.PostAsJsonAsync(requestUri, payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
            return body;

        throw new InvalidOperationException(
            $"Centra GraphQL svarade med {(int)response.StatusCode} mot {requestUri}: {TrimForError(body)}");
    }

    private static string TrimForError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "(empty)";

        var compact = body.ReplaceLineEndings(" ").Trim();
        return compact.Length <= 800 ? compact : compact[..800];
    }
}

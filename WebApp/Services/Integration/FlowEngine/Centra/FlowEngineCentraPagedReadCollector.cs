using System.Globalization;
using System.Text.Json;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraPagedReadCollector
{
    private const int DefaultPageSize = 50;

    private readonly IFlowEngineCentraGraphQlClient _centraGraphQlClient;

    public FlowEngineCentraPagedReadCollector(IFlowEngineCentraGraphQlClient centraGraphQlClient)
    {
        _centraGraphQlClient = centraGraphQlClient;
    }

    internal async Task<CentraPagedCollectionResult> CollectAsync(
        IntegrationSourceConfig centraConfig,
        string query,
        string operationName,
        string arrayFieldName,
        DateTime dateUtc,
        CancellationToken cancellationToken)
    {
        var items = new List<JsonElement>();
        var errors = new List<JsonElement>();
        var page = 1;

        while (true)
        {
            var body = await _centraGraphQlClient.PostAsync(
                centraConfig,
                new
                {
                    query,
                    operationName,
                    variables = new
                    {
                        from = dateUtc.Date.ToString("yyyy-MM-dd'T'00:00:00'Z'", CultureInfo.InvariantCulture),
                        to = dateUtc.Date.ToString("yyyy-MM-dd'T'23:59:59'Z'", CultureInfo.InvariantCulture),
                        limit = DefaultPageSize,
                        page
                    }
                },
                cancellationToken);

            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("errors", out var errorsElement) &&
                errorsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var error in errorsElement.EnumerateArray())
                    errors.Add(error.Clone());
            }

            var pageItems = ExtractDataArray(document.RootElement, arrayFieldName);
            if (pageItems.Count == 0)
                break;

            items.AddRange(pageItems);
            if (pageItems.Count < DefaultPageSize)
                break;

            page++;
        }

        return new CentraPagedCollectionResult(items, errors);
    }

    private static List<JsonElement> ExtractDataArray(JsonElement root, string arrayFieldName)
    {
        if (!root.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Object)
            return new List<JsonElement>();

        if (!dataElement.TryGetProperty(arrayFieldName, out var arrayElement) || arrayElement.ValueKind != JsonValueKind.Array)
            return new List<JsonElement>();

        var result = new List<JsonElement>();
        foreach (var item in arrayElement.EnumerateArray())
            result.Add(item.Clone());

        return result;
    }
}

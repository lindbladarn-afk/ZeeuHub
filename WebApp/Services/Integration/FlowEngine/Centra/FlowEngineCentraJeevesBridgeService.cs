using System.Globalization;
using System.Net;
using System.Text.Json;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraJeevesBridgeService : IFlowEngineCentraJeevesBridgeService
{
    private const string DuplicateOrderMarker = "__jeeves_duplicate_ohd_primary_key__";
    private const string DeadlockMarker = "__jeeves_sql_deadlock__";

    private readonly IFlowEngineJeevesApiClient _jeevesApiClient;

    public FlowEngineCentraJeevesBridgeService(
        IFlowEngineJeevesApiClient jeevesApiClient)
    {
        _jeevesApiClient = jeevesApiClient;
    }

    public IntegrationSourceConfig ResolveConfig(Guid companyId, string operationLabel)
        => _jeevesApiClient.ResolveConfig(companyId, operationLabel);

    public async Task<FlowEngineJeevesOrderCheckResult> CheckOrderAsync(
        Guid companyId,
        IntegrationSourceConfig config,
        string externalOrderNumber,
        CancellationToken cancellationToken)
    {
        var uri = _jeevesApiClient.BuildRequestUri(
            config.BaseUrl!,
            "orders",
            new Dictionary<string, string?>
            {
                ["c_foretagkod"] = "1",
                ["c_extordernr"] = externalOrderNumber
            });

        var response = await _jeevesApiClient.SendAuthorizedAsync(companyId, config, HttpMethod.Get, uri, null, cancellationToken);
        response = response with { Body = NormalizeJeevesBody(response.Body) };
        if (response.StatusCode == HttpStatusCode.NotFound)
            return new FlowEngineJeevesOrderCheckResult { Status = FlowEngineJeevesLookupStatus.NotFound };

        if ((int)response.StatusCode is < 200 or > 299)
        {
            return new FlowEngineJeevesOrderCheckResult
            {
                Status = FlowEngineJeevesLookupStatus.Error,
                ErrorMessage = $"Jeeves shipment check svarade med {(int)response.StatusCode}: {TrimForError(response.Body)}"
            };
        }

        var rows = ParseLookupRows(response.Body);
        var first = rows.FirstOrDefault();
        if (first is null)
            return new FlowEngineJeevesOrderCheckResult { Status = FlowEngineJeevesLookupStatus.NotFound };

        return new FlowEngineJeevesOrderCheckResult
        {
            Status = FlowEngineJeevesLookupStatus.Found,
            JeevesOrderStatus = ExtractInt(first, "c_ordstat", "ordstat", "ordStat", "orderStatus") ?? 0,
            JeevesOrderNumber = ExtractInt(first, "c_ordernr", "ordernr", "orderNr", "orderNumber"),
            StatusName = ExtractString(first, "c_ordstatnamn", "ordstatusname", "ordStatBeskr", "statusName"),
            TrackingUrl = ExtractString(first, "egetAttribut3", "egetattribut3", "c_egetattribut3", "c_egetAttribut3", "trackingUrl", "trackingURL")
        };
    }

    public async Task<bool> OrderExistsAsync(
        Guid companyId,
        IntegrationSourceConfig config,
        int companyCode,
        string externalOrderId,
        CancellationToken cancellationToken)
    {
        var uri = _jeevesApiClient.BuildRequestUri(
            config.BaseUrl!,
            "orders",
            new Dictionary<string, string?>
            {
                ["c_foretagkod"] = companyCode.ToString(CultureInfo.InvariantCulture),
                ["c_extordernr"] = externalOrderId
            });

        var response = await _jeevesApiClient.SendAuthorizedAsync(companyId, config, HttpMethod.Get, uri, null, cancellationToken);
        response = response with { Body = NormalizeJeevesBody(response.Body) };
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        if ((int)response.StatusCode is < 200 or > 299)
            throw new InvalidOperationException($"Jeeves exists-check svarade med {(int)response.StatusCode}: {TrimForError(response.Body)}");

        return HasAnyRows(response.Body);
    }

    public async Task CreateOrderAsync(
        Guid companyId,
        IntegrationSourceConfig config,
        string jsonPayload,
        string operationLabel,
        CancellationToken cancellationToken)
    {
        var uri = _jeevesApiClient.BuildRequestUri(config.BaseUrl!, "ordersedi", null);
        var response = await _jeevesApiClient.SendAuthorizedAsync(companyId, config, HttpMethod.Post, uri, jsonPayload, cancellationToken);
        response = response with { Body = NormalizeJeevesBody(response.Body) };

        if (string.Equals(response.Body, DuplicateOrderMarker, StringComparison.Ordinal))
            throw new FlowEngineCentraJeevesDuplicateOrderException();

        if ((int)response.StatusCode is < 200 or > 299)
        {
            if (string.Equals(response.Body, DeadlockMarker, StringComparison.Ordinal))
                throw new InvalidOperationException("Jeeves deadlock");

            throw new InvalidOperationException($"Jeeves {operationLabel} svarade med {(int)response.StatusCode}: {TrimForError(response.Body)}");
        }
    }

    private static bool HasAnyRows(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(body);
            return CountRows(document.RootElement) > 0;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static int CountRows(System.Text.Json.JsonElement element)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
            return element.GetArrayLength();

        if (element.ValueKind != System.Text.Json.JsonValueKind.Object)
            return 0;

        foreach (var key in new[] { "orders", "Orders", "items", "Items", "data", "Data", "value", "rows" })
        {
            if (element.TryGetProperty(key, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.Array)
                return value.GetArrayLength();
        }

        return 1;
    }

    private static IReadOnlyList<Dictionary<string, JsonElement>> ParseLookupRows(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return Array.Empty<Dictionary<string, JsonElement>>();

        try
        {
            using var document = JsonDocument.Parse(body);
            return ParseRows(document.RootElement);
        }
        catch (JsonException)
        {
            return Array.Empty<Dictionary<string, JsonElement>>();
        }
    }

    private static IReadOnlyList<Dictionary<string, JsonElement>> ParseRows(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object).Select(ToDictionary).ToList();

        if (root.ValueKind != JsonValueKind.Object)
            return Array.Empty<Dictionary<string, JsonElement>>();

        foreach (var key in new[] { "orders", "Orders", "items", "Items", "data", "Data" })
        {
            if (root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array)
                return value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object).Select(ToDictionary).ToList();
        }

        return new[] { ToDictionary(root) };
    }

    private static Dictionary<string, JsonElement> ToDictionary(JsonElement element)
    {
        var dictionary = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
            dictionary[property.Name] = property.Value.Clone();
        return dictionary;
    }

    private static int? ExtractInt(Dictionary<string, JsonElement> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!row.TryGetValue(key, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
                return numeric;
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric))
                return numeric;
        }

        return null;
    }

    private static string? ExtractString(Dictionary<string, JsonElement> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!row.TryGetValue(key, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.String)
                return value.GetString();
            if (value.ValueKind == JsonValueKind.Number)
                return value.GetRawText();
        }

        return null;
    }

    private static string NormalizeJeevesBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return body;

        var lowered = body.ToLowerInvariant();
        if (lowered.Contains("primary key constraint 'ohd_primary_key'", StringComparison.Ordinal) ||
            lowered.Contains("duplicate key in object 'dbo.ohd'", StringComparison.Ordinal))
        {
            return DuplicateOrderMarker;
        }

        if (lowered.Contains("deadlocked on lock resources", StringComparison.Ordinal) ||
            lowered.Contains("errornumber = 1205", StringComparison.Ordinal) ||
            lowered.Contains("has been chosen as the deadlock victim", StringComparison.Ordinal) ||
            lowered.Contains("deadlock victim", StringComparison.Ordinal) ||
            lowered.Contains("rerun the transaction", StringComparison.Ordinal))
        {
            return DeadlockMarker;
        }

        return body;
    }

    private static string TrimForError(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Empty response body";
        var trimmed = value.Trim();
        return trimmed.Length <= 320 ? trimmed : trimmed[..320];
    }
}

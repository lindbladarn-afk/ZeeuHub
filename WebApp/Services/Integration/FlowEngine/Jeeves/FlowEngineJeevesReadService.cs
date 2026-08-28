using System.Net;
using System.Text.Json;
using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineJeevesReadService : IFlowEngineJeevesReadService
{
    private static readonly JsonSerializerOptions PrettyJsonOptions = new() { WriteIndented = true };

    private static readonly HashSet<int> ImportableArtStatusCodes = new() { 0, 8 };

    private static readonly IReadOnlyDictionary<int, string> ArtStatusLabelsByCode = new Dictionary<int, string>
    {
        [0] = "3 Saljbar",
        [2] = "0 Under utveckling",
        [3] = "1 Forbereda for produktion",
        [4] = "2 Saljbar/ ej komplett",
        [6] = "4 Saljut",
        [7] = "4 REA",
        [8] = "4 Utgaende artikel",
        [9] = "5 Utgatt ur sortimentet",
        [10] = "6 SALJSTOPPAD"
    };

    private readonly IFlowEngineJeevesApiClient _jeevesApiClient;

    public FlowEngineJeevesReadService(
        IFlowEngineJeevesApiClient jeevesApiClient)
    {
        _jeevesApiClient = jeevesApiClient;
    }

    public async Task<FlowEngineOperationExecutionData> GetCustomerAddressesAsync(
        JeevesRuntimeContext runtimeContext,
        string customerNumber,
        CancellationToken cancellationToken = default)
    {
        var normalizedCustomerNumber = NormalizeLookup(customerNumber);
        if (string.IsNullOrWhiteSpace(normalizedCustomerNumber))
            throw new InvalidOperationException("Kundnummer maste anges for Get delivery address.");

        var payload = await SendAuthorizedGetAsync(
            runtimeContext,
            "kundadresser",
            new Dictionary<string, string?>
            {
                ["c_foretagkod"] = runtimeContext.CompanyCode.ToString(),
                ["c_ftgnr"] = normalizedCustomerNumber
            },
            cancellationToken);

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Fetched {CountRows(payload)} delivery address(es)",
                $"Company: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})",
                $"Customer number: {normalizedCustomerNumber}"
            },
            JsonOutput = PrettyPrintJson(payload)
        };
    }

    public async Task<FlowEngineOperationExecutionData> GetOrdersAsync(
        JeevesRuntimeContext runtimeContext,
        int companyCode,
        string lookupField,
        string lookupValue,
        CancellationToken cancellationToken = default)
    {
        var normalizedLookupField = NormalizeLookup(lookupField)?.ToLowerInvariant();
        if (normalizedLookupField is not ("c_extordernr" or "c_ordernr"))
            throw new InvalidOperationException("Lookup-falt maste vara c_extordernr eller c_ordernr for Jeeves get-orders.");

        var normalizedLookupValue = NormalizeLookup(lookupValue);
        if (string.IsNullOrWhiteSpace(normalizedLookupValue))
            throw new InvalidOperationException("Lookup-varde maste anges for Jeeves get-orders.");

        if (companyCode is not (1 or 5))
            throw new InvalidOperationException("c_foretagkod maste vara 1 eller 5 for Jeeves get-orders.");

        var payload = await SendAuthorizedGetAsync(
            runtimeContext,
            "orders",
            new Dictionary<string, string?>
            {
                ["c_foretagkod"] = companyCode.ToString(),
                [normalizedLookupField] = normalizedLookupValue
            },
            cancellationToken);

        var responsePayload = new
        {
            companyCode,
            companyName = companyCode == 1 ? "Holdit" : "Smartline",
            query = new Dictionary<string, object?>
            {
                ["c_foretagkod"] = companyCode,
                [normalizedLookupField] = normalizedLookupValue
            },
            count = CountRows(payload),
            orders = DeserializeJson(payload)
        };

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Fetched {responsePayload.count} order row(s)",
                $"Company: {responsePayload.companyName} ({companyCode})",
                $"Lookup: {normalizedLookupField}={normalizedLookupValue}"
            },
            JsonOutput = JsonSerializer.Serialize(responsePayload, PrettyJsonOptions)
        };
    }

    public async Task<FlowEngineOperationExecutionData> OrderExistsAsync(
        JeevesRuntimeContext runtimeContext,
        string orderId,
        CancellationToken cancellationToken = default)
    {
        var normalizedOrderId = NormalizeLookup(orderId);
        if (string.IsNullOrWhiteSpace(normalizedOrderId))
            throw new InvalidOperationException("Order ID maste anges for Jeeves order-exists.");

        var payload = await SendAuthorizedGetAsync(
            runtimeContext,
            "orders",
            new Dictionary<string, string?>
            {
                ["c_extordernr"] = normalizedOrderId
            },
            cancellationToken);

        var exists = CountRows(payload) > 0;
        var responsePayload = new
        {
            orderId = normalizedOrderId,
            status = exists ? "found" : "missing"
        };

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Order {normalizedOrderId}: {responsePayload.status}",
                $"Company: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})"
            },
            JsonOutput = JsonSerializer.Serialize(responsePayload, PrettyJsonOptions)
        };
    }

    public async Task<FlowEngineOperationExecutionData> GetProductAsync(
        JeevesRuntimeContext runtimeContext,
        string articleNumber,
        CancellationToken cancellationToken = default)
    {
        var normalizedArticleNumber = NormalizeArticleLookup(articleNumber);
        if (string.IsNullOrWhiteSpace(normalizedArticleNumber))
            throw new InvalidOperationException("Artikelnummer maste anges for Get product.");

        var payload = await SendAuthorizedGetAsync(
            runtimeContext,
            "artiklar",
            new Dictionary<string, string?>
            {
                ["c_foretagkod"] = runtimeContext.CompanyCode.ToString(),
                ["c_artnr"] = normalizedArticleNumber
            },
            cancellationToken);

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Fetched {CountRows(payload)} product row(s)",
                $"Company: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})",
                $"Lookup: {normalizedArticleNumber}"
            },
            JsonOutput = PrettyPrintJson(payload)
        };
    }

    public async Task<FlowEngineOperationExecutionData> GetArtStatusAsync(
        JeevesRuntimeContext runtimeContext,
        IReadOnlyList<string> articleNumbers,
        CancellationToken cancellationToken = default)
    {
        var normalizedArticleNumbers = articleNumbers
            .Select(NormalizeArticleLookup)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedArticleNumbers.Count == 0)
            throw new InvalidOperationException("Minst ett artikelnummer maste anges for Get art status.");

        var rows = new List<FlowEngineJeevesArtStatusRow>(normalizedArticleNumbers.Count);

        foreach (var articleNumber in normalizedArticleNumbers)
        {
            var payload = await SendAuthorizedGetAsync(
                runtimeContext,
                "artiklar",
                new Dictionary<string, string?>
                {
                    ["c_foretagkod"] = runtimeContext.CompanyCode.ToString(),
                    ["c_artnr"] = articleNumber
                },
                cancellationToken);

            rows.Add(ResolveArtStatusRow(articleNumber, payload));
        }

        var okCount = rows.Count(row => row.Importable);
        var notFoundCount = rows.Count(row => row.Status == 9);
        var notOkCount = rows.Count - okCount - notFoundCount;

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Art status checks: total={rows.Count}, ok={okCount}, notFound={notFoundCount}, notOk={notOkCount}",
                $"Company: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})"
            },
            JsonOutput = JsonSerializer.Serialize(rows, PrettyJsonOptions)
        };
    }

    private async Task<string> SendAuthorizedGetAsync(
        JeevesRuntimeContext runtimeContext,
        string relativePath,
        IReadOnlyDictionary<string, string?> query,
        CancellationToken cancellationToken)
    {
        var source = _jeevesApiClient.ResolveConfig(runtimeContext.CompanyId, "FlowEngine-lasningar");
        var requestUri = _jeevesApiClient.BuildRequestUri(source.BaseUrl!, relativePath, query);
        var response = await _jeevesApiClient.SendAuthorizedAsync(runtimeContext.CompanyId, source, HttpMethod.Get, requestUri, null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return "[]";

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Jeeves svarade med {(int)response.StatusCode}: {TrimForError(response.Body)}");

        return string.IsNullOrWhiteSpace(response.Body) ? "[]" : response.Body;
    }

    private static string PrettyPrintJson(string body)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "[]" : body);
        return JsonSerializer.Serialize(document.RootElement, PrettyJsonOptions);
    }

    private static object? DeserializeJson(string body)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "[]" : body);
        return JsonSerializer.Deserialize<object>(document.RootElement.GetRawText());
    }

    private static int CountRows(string body)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "[]" : body);
        return CountRows(document.RootElement);
    }

    private static int CountRows(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return element.GetArrayLength();

        if (element.ValueKind != JsonValueKind.Object)
            return 0;

        foreach (var key in new[] { "artiklar", "Artiklar", "products", "Products", "items", "Items", "data", "Data", "addresses", "Addresses" })
        {
            if (element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array)
                return value.GetArrayLength();
        }

        return 1;
    }

    private static FlowEngineJeevesArtStatusRow ResolveArtStatusRow(string articleNumber, string body)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "[]" : body);
        var rows = ExtractRows(document.RootElement);
        if (rows.Count == 0)
        {
            return new FlowEngineJeevesArtStatusRow
            {
                ArticleNumber = articleNumber,
                Status = 9,
                StatusDescription = "Not found",
                Importable = false
            };
        }

        var firstRow = rows[0];
        var name = GetString(firstRow, "itemName")
            ?? GetString(firstRow, "ItemName")
            ?? GetString(firstRow, "artBeskr")
            ?? GetString(firstRow, "ArtBeskr")
            ?? string.Empty;
        var status = GetInt(firstRow, "itemStatusCode")
            ?? GetInt(firstRow, "ItemStatusCode")
            ?? GetInt(firstRow, "status")
            ?? GetInt(firstRow, "Status");

        var importable = status.HasValue && ImportableArtStatusCodes.Contains(status.Value);
        var statusDescription = status.HasValue
            ? ResolveArtStatusLabel(status.Value)
            : "No status code";

        return new FlowEngineJeevesArtStatusRow
        {
            ArticleNumber = articleNumber,
            Name = name,
            Status = status,
            StatusDescription = statusDescription,
            Importable = importable
        };
    }

    private static List<JsonElement> ExtractRows(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().Select(element => element.Clone()).ToList();

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "artiklar", "Artiklar", "products", "Products", "items", "Items", "data", "Data" })
            {
                if (root.TryGetProperty(key, out var rows) && rows.ValueKind == JsonValueKind.Array)
                    return rows.EnumerateArray().Select(element => element.Clone()).ToList();
            }

            return new List<JsonElement> { root.Clone() };
        }

        return new List<JsonElement>();
    }

    private static string ResolveArtStatusLabel(int status)
    {
        return ArtStatusLabelsByCode.TryGetValue(status, out var label)
            ? label
            : $"Status {status}";
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
            return number;

        if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out number))
            return number;

        return null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.String)
            return property.GetString();

        return property.ValueKind == JsonValueKind.Number ? property.GetRawText() : null;
    }

    private static string TrimForError(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Empty response body";

        var trimmed = value.Trim();
        return trimmed.Length <= 220 ? trimmed : trimmed[..220];
    }

    private static string? NormalizeLookup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string? NormalizeArticleLookup(string? raw)
    {
        var trimmed = NormalizeLookup(raw);
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        if (!trimmed.Any(char.IsDigit))
            return trimmed;

        var compact = trimmed.Replace(" ", string.Empty).Replace("\u00A0", string.Empty);
        if (compact.Length == 13 &&
            compact.StartsWith("7330985", StringComparison.Ordinal) &&
            compact.All(char.IsDigit))
        {
            return compact[7..12];
        }

        if (compact.EndsWith(".0", StringComparison.Ordinal) &&
            compact[..^2].All(char.IsDigit))
        {
            return compact[..^2];
        }

        return compact;
    }
}

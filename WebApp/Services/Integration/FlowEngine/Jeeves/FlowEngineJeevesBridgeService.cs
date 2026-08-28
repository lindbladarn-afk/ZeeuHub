using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineJeevesBridgeService : IFlowEngineJeevesBridgeService
{
    private const string JeevesDuplicateOrderMarker = "__jeeves_duplicate_ohd_primary_key__";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly IFlowEngineJeevesApiClient _jeevesApiClient;

    public FlowEngineJeevesBridgeService(
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
        CancellationToken cancellationToken = default)
    {
        var baseUrl = config.BaseUrl!.TrimEnd('/');
        var uri = $"{baseUrl}/api/entity/order?fields=c_extordernr,c_ordstat,c_ordnr&query={Uri.EscapeDataString($"c_extordernr eq '{externalOrderNumber}'")}";

        var response = await _jeevesApiClient.SendAuthorizedAsync(companyId, config, HttpMethod.Get, uri, null, cancellationToken);
        response = response with { Body = NormalizeJeevesBody(response.Body) };

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new FlowEngineJeevesOrderCheckResult { Status = FlowEngineJeevesLookupStatus.NotFound };

        if (!response.IsSuccessStatusCode)
        {
            return new FlowEngineJeevesOrderCheckResult
            {
                Status = FlowEngineJeevesLookupStatus.Error,
                ErrorMessage = $"Jeeves svarade med {(int)response.StatusCode}: {TrimForError(response.Body)}"
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
            JeevesOrderNumber = ExtractInt(first, "c_ordnr", "ordnr", "orderNumber"),
            StatusName = ExtractString(first, "statusName", "c_statusnamn", "statusnamn", "orderStatusName")
        };
    }

    public async Task<bool> OrderExistsAsync(
        IntegrationSourceConfig config,
        Guid companyId,
        int jeevesCompanyCode,
        string externalOrderNumber,
        CancellationToken cancellationToken = default)
    {
        var uri = _jeevesApiClient.BuildRequestUri(
            config.BaseUrl!,
            "orders",
            new Dictionary<string, string?>
            {
                ["c_foretagkod"] = jeevesCompanyCode.ToString(CultureInfo.InvariantCulture),
                ["c_extordernr"] = externalOrderNumber
            });

        var response = await _jeevesApiClient.SendAuthorizedAsync(companyId, config, HttpMethod.Get, uri, null, cancellationToken);
        response = response with { Body = NormalizeJeevesBody(response.Body) };
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Jeeves exists-check svarade med {(int)response.StatusCode}: {TrimForError(response.Body)}");

        return ParseLookupRows(response.Body).Count > 0;
    }

    public async Task CreateOrderAsync(
        IntegrationSourceConfig config,
        Guid companyId,
        FlowEngineShopifyJeevesOrderPayload payload,
        CancellationToken cancellationToken = default)
    {
        var uri = _jeevesApiClient.BuildRequestUri(config.BaseUrl!, "ordersedi", null);
        var json = JsonSerializer.Serialize(ToJeevesCreateOrderRequest(payload), JsonOptions);
        var response = await _jeevesApiClient.SendAuthorizedAsync(companyId, config, HttpMethod.Post, uri, json, cancellationToken);
        response = response with { Body = NormalizeJeevesBody(response.Body) };

        if (ContainsDuplicateOrderMarker(response.Body))
            throw new FlowEngineJeevesDuplicateOrderException();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Jeeves send orders svarade med {(int)response.StatusCode}: {TrimForError(response.Body)}");
    }

    private static bool ContainsDuplicateOrderMarker(string body)
        => string.Equals(body, JeevesDuplicateOrderMarker, StringComparison.Ordinal);

    private static string NormalizeJeevesBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;

        var trimmed = body.Trim();
        if (trimmed.Contains("ohd_ohdeid_pk", StringComparison.OrdinalIgnoreCase))
            return JeevesDuplicateOrderMarker;

        return trimmed;
    }

    private static JeevesCreateOrderRequest ToJeevesCreateOrderRequest(FlowEngineShopifyJeevesOrderPayload payload)
        => new()
        {
            CompanyCode = payload.CompanyCode,
            CustomerNumber = payload.CustomerNumber,
            ExternalOrderNumber = payload.ExternalOrderNumber,
            CustomerReference = payload.CustomerReference,
            OrderDate = payload.OrderDate,
            OrderType = payload.OrderType,
            CurrencyCode = payload.CurrencyCode,
            PartialDeliveryAllowed = payload.PartialDeliveryAllowed,
            Edit = payload.Edit,
            DeliveryName = payload.DeliveryName,
            DeliveryAddress1 = payload.DeliveryAddress1,
            DeliveryAddress2 = payload.DeliveryAddress2,
            DeliveryZipCode = payload.DeliveryZipCode,
            DeliveryCity = payload.DeliveryCity,
            DeliveryCountryCode = payload.DeliveryCountryCode,
            GoodsMark3 = payload.GoodsMark3,
            GoodsMark4 = payload.GoodsMark4,
            EgenParameter3 = payload.EgenParameter3,
            OrderLines = payload.OrderLines.Select(line => new JeevesCreateOrderLineRequest
            {
                ArticleNumber = line.ArticleNumber,
                Quantity = line.Quantity,
                Price = line.Price,
                CurrencyValue = line.CurrencyValue,
                CustomerDiscount = line.CustomerDiscount,
                OrderDiscount = line.OrderDiscount,
                Discount1 = line.Discount1,
                Discount2 = line.Discount2,
                Discount3 = line.Discount3,
                Edit = line.Edit
            }).ToList()
        };

    private static string TrimForError(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        return trimmed.Length <= 300 ? trimmed : trimmed[..300];
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
        {
            return root.EnumerateArray()
                .Where(element => element.ValueKind == JsonValueKind.Object)
                .Select(CloneObject)
                .ToList();
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
                return rows.EnumerateArray().Where(element => element.ValueKind == JsonValueKind.Object).Select(CloneObject).ToList();

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                return data.EnumerateArray().Where(element => element.ValueKind == JsonValueKind.Object).Select(CloneObject).ToList();
        }

        return Array.Empty<Dictionary<string, JsonElement>>();
    }

    private static Dictionary<string, JsonElement> CloneObject(JsonElement element)
    {
        var clone = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
            clone[property.Name] = property.Value.Clone();
        return clone;
    }

    private static string? ExtractString(IReadOnlyDictionary<string, JsonElement> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!row.TryGetValue(key, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.String)
                return Normalize(value.GetString());

            if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                return Normalize(value.ToString());
        }

        return null;
    }

    private static int? ExtractInt(IReadOnlyDictionary<string, JsonElement> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!row.TryGetValue(key, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String &&
                int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }
        }

        return null;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class JeevesCreateOrderRequest
    {
        [JsonPropertyName("c_foretagkod")]
        public int CompanyCode { get; set; }

        [JsonPropertyName("c_ftgnr")]
        public string? CustomerNumber { get; set; }

        [JsonPropertyName("c_extordernr")]
        public string ExternalOrderNumber { get; set; } = string.Empty;

        [JsonPropertyName("c_kundbestnr")]
        public string CustomerReference { get; set; } = string.Empty;

        [JsonPropertyName("c_orddatum")]
        public string? OrderDate { get; set; }

        [JsonPropertyName("c_OrdTyp")]
        public int OrderType { get; set; }

        [JsonPropertyName("c_valkod")]
        public string? CurrencyCode { get; set; }

        [JsonPropertyName("c_dellevtillaten")]
        public int PartialDeliveryAllowed { get; set; }

        [JsonPropertyName("c_edit")]
        public string Edit { get; set; } = "FlowEngine";

        [JsonPropertyName("c_ordlevadr1")]
        public string? DeliveryName { get; set; }

        [JsonPropertyName("c_ordlevadr2")]
        public string? DeliveryAddress1 { get; set; }

        [JsonPropertyName("c_ordlevadr3")]
        public string? DeliveryAddress2 { get; set; }

        [JsonPropertyName("c_ordlevadr4")]
        public string? DeliveryZipCode { get; set; }

        [JsonPropertyName("c_ordlevadrbstort")]
        public string? DeliveryCity { get; set; }

        [JsonPropertyName("c_ordlevadrlandskod")]
        public string? DeliveryCountryCode { get; set; }

        [JsonPropertyName("c_godsMarke3")]
        public string? GoodsMark3 { get; set; }

        [JsonPropertyName("c_godsMarke4")]
        public string? GoodsMark4 { get; set; }

        [JsonPropertyName("c_egenparameter3")]
        public string? EgenParameter3 { get; set; }

        [JsonPropertyName("OrderRader")]
        public List<JeevesCreateOrderLineRequest> OrderLines { get; set; } = new();
    }

    private sealed class JeevesCreateOrderLineRequest
    {
        [JsonPropertyName("c_artnr")]
        public string? ArticleNumber { get; set; }

        [JsonPropertyName("c_ordantal")]
        public decimal Quantity { get; set; }

        [JsonPropertyName("c_vb_pris")]
        public decimal? Price { get; set; }

        [JsonPropertyName("c_valkod")]
        public int CurrencyValue { get; set; }

        [JsonPropertyName("c_kundrabatt")]
        public decimal CustomerDiscount { get; set; }

        [JsonPropertyName("c_OrdRabatt")]
        public decimal OrderDiscount { get; set; }

        [JsonPropertyName("c_rabatt1")]
        public decimal? Discount1 { get; set; }

        [JsonPropertyName("c_rabatt2")]
        public decimal? Discount2 { get; set; }

        [JsonPropertyName("c_rabatt3")]
        public decimal? Discount3 { get; set; }

        [JsonPropertyName("c_edit")]
        public string Edit { get; set; } = "FlowEngine";
    }
}

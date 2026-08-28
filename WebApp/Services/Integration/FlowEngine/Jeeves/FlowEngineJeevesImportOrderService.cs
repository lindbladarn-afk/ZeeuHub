using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineJeevesImportOrderService : IFlowEngineJeevesImportOrderService
{
    private const string ImportEanPrefix = "7330985";

    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions PrettyJsonOptions = new(RequestJsonOptions)
    {
        WriteIndented = true
    };

    private readonly IFlowEngineJeevesApiClient _jeevesApiClient;
    private readonly ILogger<FlowEngineJeevesImportOrderService> _logger;

    public FlowEngineJeevesImportOrderService(
        IFlowEngineJeevesApiClient jeevesApiClient,
        ILogger<FlowEngineJeevesImportOrderService> logger)
    {
        _jeevesApiClient = jeevesApiClient;
        _logger = logger;
    }

    public async Task<FlowEngineOperationExecutionData> ExecuteAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken = default)
    {
        var preparedOrder = PrepareOrder(runtimeContext, request);
        var payload = BuildPayload(preparedOrder);
        var payloadJson = JsonSerializer.Serialize(payload, PrettyJsonOptions);
        var isDryRun = request.Flags.DryRun;

        var result = new FlowEngineOperationExecutionData
        {
            JsonOutput = payloadJson
        };

        result.SummaryLines.Add($"Company: {runtimeContext.CompanyName} ({preparedOrder.CompanyCode})");
        result.SummaryLines.Add($"Customer: {preparedOrder.CustomerNumber}");
        result.SummaryLines.Add($"External order number: {preparedOrder.ExternalOrderNumber}");
        result.SummaryLines.Add($"Lines: {preparedOrder.Lines.Count}");

        if (isDryRun)
        {
            result.SummaryLines.Insert(0, $"Order {preparedOrder.ExternalOrderNumber} mapped successfully (dry run)");
            return result;
        }

        var config = ResolveConfig(runtimeContext);
        var exists = await OrderExistsAsync(config, runtimeContext.CompanyId, preparedOrder, cancellationToken);
        if (exists)
        {
            result.SummaryLines.Insert(0, $"Order {preparedOrder.ExternalOrderNumber} already exists in Jeeves");
            return result;
        }

        await CreateOrderAsync(config, runtimeContext.CompanyId, payload, preparedOrder.ExternalOrderNumber, cancellationToken);
        result.SummaryLines.Insert(0, $"Order {preparedOrder.ExternalOrderNumber} sent successfully");
        return result;
    }

    private PreparedImportOrder PrepareOrder(JeevesRuntimeContext runtimeContext, FlowEngineExecuteJobRequest request)
    {
        var input = request.Params.JeevesImportOrder
            ?? throw new InvalidOperationException("Import order saknar orderpayload.");

        var companyCode = input.CompanyCode.GetValueOrDefault(runtimeContext.CompanyCode);
        if (companyCode <= 0)
            throw new InvalidOperationException("Import order maste ha ett giltigt bolagskodsvarde.");

        var customerNumber = RequiredText(input.CustomerNumber, "Kundnummer maste anges for import order.");
        var orderType = input.OrderType.GetValueOrDefault(1);
        if (orderType <= 0)
            throw new InvalidOperationException("Ordertyp maste vara ett positivt heltal.");

        var externalOrderNumber = RequiredText(
            input.ExternalOrderNumber,
            "Externt ordernummer maste anges for import order.");

        var populatedLines = input.Lines
            .Select((line, index) => new { Line = line, Index = index + 1 })
            .Where(entry =>
                !string.IsNullOrWhiteSpace(entry.Line.ArticleNumber) ||
                !string.IsNullOrWhiteSpace(entry.Line.Quantity) ||
                !string.IsNullOrWhiteSpace(entry.Line.Price))
            .ToList();

        if (populatedLines.Count == 0)
            throw new InvalidOperationException("Import order maste innehalla minst en orderrad.");

        var lines = populatedLines
            .Select(entry => PrepareLine(entry.Line, entry.Index))
            .ToList();

        return new PreparedImportOrder
        {
            CompanyCode = companyCode,
            CustomerNumber = customerNumber,
            OrderType = orderType,
            CustomerReference = NormalizeOptional(input.CustomerReference) ?? externalOrderNumber,
            ExternalOrderNumber = externalOrderNumber,
            DeliveryPlaceCode = NormalizeOptional(input.DeliveryPlaceCode),
            Lines = lines
        };
    }

    private static PreparedImportOrderLine PrepareLine(FlowEngineJeevesImportLineInput line, int rowNumber)
    {
        var articleNumber = NormalizeImportArticleNumber(
            RequiredText(line.ArticleNumber, $"Artikelnummer saknas pa rad {rowNumber}."));

        var quantity = ParseDecimal(line.Quantity, "antal", rowNumber);
        if (quantity == 0)
            throw new InvalidOperationException($"Antal pa rad {rowNumber} kan inte vara 0.");

        decimal? price = null;
        if (!string.IsNullOrWhiteSpace(line.Price))
        {
            price = ParseDecimal(line.Price, "pris", rowNumber);
            if (price.Value < 0)
                throw new InvalidOperationException($"Pris pa rad {rowNumber} kan inte vara negativt.");
        }

        return new PreparedImportOrderLine
        {
            ArticleNumber = articleNumber,
            Quantity = quantity,
            Price = price
        };
    }

    private IntegrationSourceConfig ResolveConfig(JeevesRuntimeContext runtimeContext)
    {
        return _jeevesApiClient.ResolveConfig(runtimeContext.CompanyId, "FlowEngine import order");
    }

    private async Task<bool> OrderExistsAsync(
        IntegrationSourceConfig config,
        Guid companyId,
        PreparedImportOrder order,
        CancellationToken cancellationToken)
    {
        var uri = _jeevesApiClient.BuildRequestUri(
            config.BaseUrl!,
            "orders",
            new Dictionary<string, string?>
            {
                ["c_foretagkod"] = order.CompanyCode.ToString(CultureInfo.InvariantCulture),
                ["c_extordernr"] = order.ExternalOrderNumber
            });

        var response = await _jeevesApiClient.SendAuthorizedAsync(companyId, config, HttpMethod.Get, uri, null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Jeeves exists-check svarade med {(int)response.StatusCode}: {TrimForError(response.Body)}");

        return HasAnyRows(response.Body);
    }

    private async Task CreateOrderAsync(
        IntegrationSourceConfig config,
        Guid companyId,
        JeevesImportOrderCreateRequest payload,
        string externalOrderNumber,
        CancellationToken cancellationToken)
    {
        var uri = _jeevesApiClient.BuildRequestUri(config.BaseUrl!, "ordersedi", null);
        var json = JsonSerializer.Serialize(payload, RequestJsonOptions);
        var response = await _jeevesApiClient.SendAuthorizedAsync(companyId, config, HttpMethod.Post, uri, json, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "FlowEngine import-order failed for {ExternalOrderNumber}. Status {StatusCode}.",
                externalOrderNumber,
                (int)response.StatusCode);
            throw new InvalidOperationException($"Jeeves import order svarade med {(int)response.StatusCode}: {TrimForError(response.Body)}");
        }
    }

    private static JeevesImportOrderCreateRequest BuildPayload(PreparedImportOrder order)
    {
        return new JeevesImportOrderCreateRequest
        {
            CompanyCode = order.CompanyCode,
            CustomerNumber = order.CustomerNumber,
            ExternalOrderNumber = order.ExternalOrderNumber,
            CustomerReference = order.CustomerReference,
            OrderDate = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            OrderType = order.OrderType,
            DeliveryPlaceCode = order.DeliveryPlaceCode,
            PartialDeliveryAllowed = 0,
            Edit = "FlowEngine",
            PaidAmount = ResolvePaidAmount(order.Lines),
            OrderLines = order.Lines.Select(line => new JeevesImportOrderCreateLine
            {
                ArticleNumber = line.ArticleNumber,
                Quantity = line.Quantity,
                Price = line.Price,
                CurrencyCode = 0,
                CustomerDiscount = 0,
                OrderDiscount = 0,
                Discount1 = 0,
                Discount2 = 0,
                Discount3 = 0,
                Edit = "FlowEngine"
            }).ToList()
        };
    }

    private static string ResolvePaidAmount(IReadOnlyList<PreparedImportOrderLine> lines)
    {
        if (!lines.Any(line => line.Price.HasValue))
            return "0,00";

        var total = lines.Aggregate(decimal.Zero, (current, line) => current + (line.Quantity * (line.Price ?? 0m)));
        return total.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static bool HasAnyRows(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;

        try
        {
            using var document = JsonDocument.Parse(body);
            return CountRows(document.RootElement) > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int CountRows(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return element.GetArrayLength();

        if (element.ValueKind != JsonValueKind.Object)
            return 0;

        foreach (var key in new[] { "orders", "Orders", "items", "Items", "data", "Data" })
        {
            if (element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array)
                return value.GetArrayLength();
        }

        return 1;
    }

    private static string NormalizeImportArticleNumber(string raw)
    {
        var trimmedRaw = raw.Trim();
        var expanded = ExpandScientificNotationIfNeeded(trimmedRaw) ?? trimmedRaw;
        return ExtractArticleNumberFromEan(expanded) ?? expanded;
    }

    private static string? ExpandScientificNotationIfNeeded(string raw)
    {
        var compact = raw.Replace("\u00A0", string.Empty).Replace(" ", string.Empty);
        if (compact.IndexOf('e') < 0 && compact.IndexOf('E') < 0)
            return null;

        var normalized = compact;
        if (normalized.Contains(',') && !normalized.Contains('.'))
            normalized = normalized.Replace(",", ".", StringComparison.Ordinal);
        else if (normalized.Contains(',') && normalized.Contains('.'))
            normalized = normalized.Replace(",", string.Empty, StringComparison.Ordinal);

        if (!decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
            parsed <= 0 ||
            decimal.Truncate(parsed) != parsed)
        {
            return null;
        }

        var plain = parsed.ToString("0", CultureInfo.InvariantCulture);
        return plain.IndexOf('e') >= 0 || plain.IndexOf('E') >= 0 ? null : plain;
    }

    private static string? ExtractArticleNumberFromEan(string raw)
    {
        var compact = raw.Replace("\u00A0", string.Empty).Replace(" ", string.Empty);
        if (compact.Length != 13 ||
            !compact.StartsWith(ImportEanPrefix, StringComparison.Ordinal) ||
            !compact.All(char.IsDigit) ||
            !IsValidEan13(compact))
        {
            return null;
        }

        return compact[ImportEanPrefix.Length..^1];
    }

    private static bool IsValidEan13(string ean)
    {
        var digits = ean.Select(ch => ch - '0').ToArray();
        if (digits.Length != 13)
            return false;

        var checksumBase = 0;
        for (var index = 0; index < 12; index++)
            checksumBase += digits[index] * (index % 2 == 0 ? 1 : 3);

        var expectedCheckDigit = (10 - (checksumBase % 10)) % 10;
        return digits[12] == expectedCheckDigit;
    }

    private static decimal ParseDecimal(string raw, string fieldName, int rowNumber)
    {
        var normalized = NormalizeDecimalString((raw ?? string.Empty).Trim());
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException($"{fieldName} maste anges pa rad {rowNumber}.");

        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidOperationException($"Ogiltigt {fieldName} pa rad {rowNumber}: '{raw}'.");

        return parsed;
    }

    private static string NormalizeDecimalString(string value)
    {
        var candidate = value
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\u2212", "-", StringComparison.Ordinal)
            .Replace("\u2012", "-", StringComparison.Ordinal)
            .Replace("\u2013", "-", StringComparison.Ordinal)
            .Replace("\u2014", "-", StringComparison.Ordinal)
            .Replace("\u2015", "-", StringComparison.Ordinal)
            .Replace("\uFE63", "-", StringComparison.Ordinal)
            .Replace("\uFF0D", "-", StringComparison.Ordinal);

        var hasComma = candidate.Contains(',');
        var hasDot = candidate.Contains('.');

        if (hasComma && hasDot)
        {
            var lastComma = candidate.LastIndexOf(',');
            var lastDot = candidate.LastIndexOf('.');
            if (lastComma > lastDot)
            {
                candidate = candidate.Replace(".", string.Empty, StringComparison.Ordinal);
                candidate = candidate.Replace(",", ".", StringComparison.Ordinal);
            }
            else
            {
                candidate = candidate.Replace(",", string.Empty, StringComparison.Ordinal);
            }
        }
        else if (hasComma)
        {
            candidate = candidate.Replace(",", ".", StringComparison.Ordinal);
        }

        return candidate;
    }

    private static string RequiredText(string? value, string errorMessage)
    {
        var normalized = NormalizeOptional(value);
        return normalized ?? throw new InvalidOperationException(errorMessage);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string TrimForError(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Empty response body";

        var trimmed = value.Trim();
        return trimmed.Length <= 320 ? trimmed : trimmed[..320];
    }

    private sealed class PreparedImportOrder
    {
        public int CompanyCode { get; set; }
        public string CustomerNumber { get; set; } = string.Empty;
        public int OrderType { get; set; }
        public string CustomerReference { get; set; } = string.Empty;
        public string ExternalOrderNumber { get; set; } = string.Empty;
        public string? DeliveryPlaceCode { get; set; }
        public List<PreparedImportOrderLine> Lines { get; set; } = new();
    }

    private sealed class PreparedImportOrderLine
    {
        public string ArticleNumber { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal? Price { get; set; }
    }

    private sealed class JeevesImportOrderCreateRequest
    {
        [JsonPropertyName("c_foretagkod")]
        public int CompanyCode { get; set; }

        [JsonPropertyName("c_ftgnr")]
        public string CustomerNumber { get; set; } = string.Empty;

        [JsonPropertyName("c_extordernr")]
        public string ExternalOrderNumber { get; set; } = string.Empty;

        [JsonPropertyName("c_kundbestnr")]
        public string CustomerReference { get; set; } = string.Empty;

        [JsonPropertyName("c_orddatum")]
        public string? OrderDate { get; set; }

        [JsonPropertyName("c_OrdTyp")]
        public int OrderType { get; set; }

        [JsonPropertyName("c_dellevtillaten")]
        public int PartialDeliveryAllowed { get; set; }

        [JsonPropertyName("c_edit")]
        public string Edit { get; set; } = "FlowEngine";

        [JsonPropertyName("c_ordlevplats1")]
        public string? DeliveryPlaceCode { get; set; }

        [JsonPropertyName("c_egenparameter3")]
        public string? PaidAmount { get; set; }

        [JsonPropertyName("OrderRader")]
        public List<JeevesImportOrderCreateLine> OrderLines { get; set; } = new();
    }

    private sealed class JeevesImportOrderCreateLine
    {
        [JsonPropertyName("c_artnr")]
        public string ArticleNumber { get; set; } = string.Empty;

        [JsonPropertyName("c_ordantal")]
        public decimal Quantity { get; set; }

        [JsonPropertyName("c_vb_pris")]
        public decimal? Price { get; set; }

        [JsonPropertyName("c_valkod")]
        public int CurrencyCode { get; set; }

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

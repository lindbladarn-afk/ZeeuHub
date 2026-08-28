using System.Text.Json;
using WebApp.Helpers;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineImportOrderWorkflowService : IFlowEngineImportOrderWorkflowService
{
    private const string FlowEngineImportOrderSessionKey = "FlowEngine.ImportOrder";

    private static readonly HashSet<string> ImportArticleHeaderLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "artnr",
        "art nr",
        "artikelnummer",
        "artikel nr",
        "article",
        "article number",
        "article no",
        "ean",
        "sku",
        "item",
        "item no"
    };

    private static readonly HashSet<string> ImportQuantityHeaderLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "ant",
        "antal",
        "ant/bas",
        "ant bas",
        "qty",
        "quantity",
        "kvantitet"
    };

    private static readonly HashSet<string> ImportPriceHeaderLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "pris",
        "price",
        "vb pris",
        "vb_pris"
    };

    private readonly IHttpContextAccessor _httpContextAccessor;

    public FlowEngineImportOrderWorkflowService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public FlowEngineImportOrderSessionState? LoadState()
        => _httpContextAccessor.HttpContext?.Session.Get<FlowEngineImportOrderSessionState>(FlowEngineImportOrderSessionKey);

    public void SaveState(FlowEngineImportOrderSessionState state)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session is null)
            return;

        session.Set(FlowEngineImportOrderSessionKey, state);
    }

    public FlowEngineRunImportOrderInput NormalizeInput(FlowEngineRunImportOrderInput input)
    {
        return new FlowEngineRunImportOrderInput
        {
            CustomerNumber = input.CustomerNumber?.Trim() ?? string.Empty,
            OrderType = input.OrderType <= 0 ? 1 : input.OrderType,
            CustomerReference = input.CustomerReference?.Trim() ?? string.Empty,
            ExternalOrderNumber = input.ExternalOrderNumber?.Trim() ?? string.Empty,
            DeliveryPlaceCode = input.DeliveryPlaceCode?.Trim() ?? string.Empty,
            Lines = input.Lines ?? string.Empty,
            DryRun = input.DryRun
        };
    }

    public FlowEngineImportOrderSessionState BuildState(
        FlowEngineRunImportOrderInput form,
        FlowEngineImportOrderSessionState? currentState,
        IReadOnlyCollection<FlowEngineDeliveryAddressOption>? deliveryAddressOptions = null,
        FlowEngineImportAddressLookupContext? addressLookupContext = null,
        FlowEngineImportDocumentReview? documentReview = null,
        IReadOnlyCollection<FlowEngineJeevesArtStatusRow>? artStatusRows = null)
    {
        return new FlowEngineImportOrderSessionState
        {
            Form = form,
            DeliveryAddressOptions = deliveryAddressOptions?.ToList()
                ?? currentState?.DeliveryAddressOptions
                ?? new List<FlowEngineDeliveryAddressOption>(),
            AddressLookupContext = addressLookupContext ?? currentState?.AddressLookupContext,
            DocumentReview = documentReview,
            ArtStatusRows = artStatusRows?.ToList()
                ?? currentState?.ArtStatusRows
                ?? new List<FlowEngineJeevesArtStatusRow>()
        };
    }

    public string? ResolveDeliveryPlaceCode(int companyCode, string customerNumber, string? selectedCode)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(selectedCode) ? null : selectedCode.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode))
            return null;

        var state = LoadState();
        var lookup = state?.AddressLookupContext;
        if (lookup is null)
            return null;

        if (!string.Equals(lookup.CustomerNumber, customerNumber.Trim(), StringComparison.Ordinal))
            return null;

        if (lookup.CompanyCode != companyCode)
            return null;

        return state?.DeliveryAddressOptions?.Any(option => option.Code == normalizedCode) == true
            ? normalizedCode
            : null;
    }

    public List<FlowEngineDeliveryAddressOption> ParseDeliveryAddressOptionsFromJob(FlowEngineJobSnapshot job)
    {
        var output = job.Result?.StandardOutput;
        if (string.IsNullOrWhiteSpace(output))
            return new List<FlowEngineDeliveryAddressOption>();

        var json = ExtractJsonPayload(output);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        JsonElement addresses;
        if (root.ValueKind == JsonValueKind.Array)
        {
            addresses = root;
        }
        else if (root.ValueKind == JsonValueKind.Object &&
                 ((root.TryGetProperty("addresses", out addresses) && addresses.ValueKind == JsonValueKind.Array) ||
                  (root.TryGetProperty("data", out addresses) && addresses.ValueKind == JsonValueKind.Array)))
        {
        }
        else
        {
            throw new InvalidOperationException("Delivery address output file had an unexpected format.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var options = new List<FlowEngineDeliveryAddressOption>();

        foreach (var item in addresses.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var code = FirstString(item, "ordLevPlats1", "ordlevplats1", "deliveryPlaceCode", "code");
            var name = FirstString(item, "ftgNamn", "ftgnamn", "name", "deliveryName");
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) || !seen.Add(code))
                continue;

            options.Add(new FlowEngineDeliveryAddressOption
            {
                Code = code,
                FtgNamn = name,
                Label = $"{code} - {name}"
            });
        }

        return options;
    }

    public FlowEngineImportDocumentReview BuildDocumentReview(string fileName, FlowEngineOrderDocumentExtractionResult extractionResult)
    {
        return new FlowEngineImportDocumentReview
        {
            FileName = SanitizeFileName(fileName),
            Source = extractionResult.Source,
            Lines = extractionResult.Lines
                .Select(line => new FlowEngineImportDocumentReviewLine
                {
                    ArticleNumber = line.ArticleNumber,
                    Quantity = line.Quantity
                })
                .ToList()
        };
    }

    public List<FlowEngineJeevesArtStatusRow> ParseArtStatusRowsFromJob(FlowEngineJobSnapshot job)
    {
        var output = job.Result?.StandardOutput;
        if (string.IsNullOrWhiteSpace(output))
            return new List<FlowEngineJeevesArtStatusRow>();

        var json = ExtractJsonPayload(output);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<FlowEngineJeevesArtStatusRow>>(root.GetRawText()) ?? new List<FlowEngineJeevesArtStatusRow>();
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<FlowEngineJeevesArtStatusRow>>(data.GetRawText()) ?? new List<FlowEngineJeevesArtStatusRow>();
        }

        return new List<FlowEngineJeevesArtStatusRow>();
    }

    public FlowEngineImportDocumentReview BuildDocumentErrorReview(string? fileName, string errorMessage)
    {
        return new FlowEngineImportDocumentReview
        {
            FileName = string.IsNullOrWhiteSpace(fileName) ? string.Empty : SanitizeFileName(fileName),
            ErrorMessage = errorMessage
        };
    }

    public string MergeDocumentLines(string currentLines, IReadOnlyCollection<FlowEngineImportDocumentReviewLine> extractedLines)
    {
        var normalizedCurrent = currentLines?
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim()
            ?? string.Empty;

        var extractedText = string.Join(
            Environment.NewLine,
            extractedLines
                .Where(line =>
                    !string.IsNullOrWhiteSpace(line.ArticleNumber) &&
                    !string.IsNullOrWhiteSpace(line.Quantity))
                .Select(line => $"{line.ArticleNumber.Trim()};{line.Quantity.Trim()}"));

        if (string.IsNullOrWhiteSpace(extractedText))
            return normalizedCurrent;

        if (string.IsNullOrWhiteSpace(normalizedCurrent))
            return extractedText;

        return $"{normalizedCurrent}{Environment.NewLine}{extractedText}";
    }

    public List<FlowEngineJeevesImportLineInput> ParseImportOrderLines(string rawLines)
    {
        var rows = rawLines
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(ParseImportOrderLine)
            .Where(parts => parts.Any(part => !string.IsNullOrWhiteSpace(part)))
            .ToList();

        if (rows.Count > 0 && LooksLikeImportHeaderRow(rows[0]))
            rows.RemoveAt(0);

        return rows
            .Select(parts => new FlowEngineJeevesImportLineInput
            {
                ArticleNumber = parts.ElementAtOrDefault(0) ?? string.Empty,
                Quantity = parts.ElementAtOrDefault(1) ?? string.Empty,
                Price = parts.ElementAtOrDefault(2)
            })
            .Where(line =>
                !string.IsNullOrWhiteSpace(line.ArticleNumber) ||
                !string.IsNullOrWhiteSpace(line.Quantity) ||
                !string.IsNullOrWhiteSpace(line.Price))
            .ToList();
    }

    private static string ExtractJsonPayload(string output)
    {
        var objectIndex = output.IndexOf('{');
        var arrayIndex = output.IndexOf('[');
        var startIndex = objectIndex switch
        {
            < 0 => arrayIndex,
            _ when arrayIndex < 0 => objectIndex,
            _ => Math.Min(objectIndex, arrayIndex)
        };

        if (startIndex < 0)
            throw new InvalidOperationException("FlowEngine-resultatet saknar JSON-payload.");

        return output[startIndex..];
    }

    private static string FirstString(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var property))
                continue;

            var value = property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.GetRawText(),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }

    private static string[] ParseImportOrderLine(string line)
    {
        var delimiter = line.Contains(';')
            ? ';'
            : line.Contains('\t')
                ? '\t'
                : ',';

        return line
            .Split(delimiter)
            .Select(part => part.Trim())
            .ToArray();
    }

    private static bool LooksLikeImportHeaderRow(IReadOnlyList<string> parts)
    {
        if (parts.Count == 0)
            return false;

        var articleHeader = parts.Count > 0 && ImportArticleHeaderLabels.Contains(parts[0]);
        var quantityHeader = parts.Count > 1 && ImportQuantityHeaderLabels.Contains(parts[1]);
        var priceHeader = parts.Count > 2 && ImportPriceHeaderLabels.Contains(parts[2]);

        return articleHeader || quantityHeader || priceHeader;
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(invalid, '_');

        return Path.GetFileName(fileName);
    }
}

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineOrderDocumentExtractionService : IFlowEngineOrderDocumentExtractionService
{
    private static readonly Regex ValidArticleNumberRegex = new(
        "^[A-Za-z0-9][A-Za-z0-9._/\\-]{0,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BaseQuantityRegex = new(
        "\\b-?\\d+(?:[.,]\\d+)?\\s+Krt\\s+à\\s+-?\\d+(?:[.,]\\d+)?\\s+(-?\\d+(?:[.,]\\d+)?)\\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BaseArticleNumberRegex = new(
        "^\\d{5,14}\\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DeliveryDateRegex = new(
        "^\\d{2}/\\d{2}/\\d{4}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex IsoDeliveryDateRegex = new(
        "^\\d{4}-\\d{2}-\\d{2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NumericTokenRegex = new(
        "^-?\\d+(?:[.,]\\d+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DecimalAmountRegex = new(
        "^-?\\d+[.,]\\d+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CurrencyTokenRegex = new(
        "^[A-Z]{3}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex IntegerTokenRegex = new(
        "^\\d+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LocationTokenRegex = new(
        "^[A-Z]\\d{2}-\\d{2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SallingPrimaryRowRegex = new(
        "^\\s*\\d+\\s+\\S+\\s+\\S+\\s+HOLD\\s*IT\\s+([A-Za-z0-9._/\\-]+)\\b.*\\s+(-?\\d+(?:[.,]\\d+)?)\\s+-?\\d+(?:[.,]\\d+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ManufacturingQuantityRegex = new(
        "^\\s*\\d+\\s+\\d{5,14}\\s+.*?\\s+(-?\\d+(?:[.,]\\d+)?)\\s+[A-Z]{1,8}\\s+\\d{4}[.-]\\d{2}[.-]\\d{2}\\s+.*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MagasinArticleRegex = new(
        "^\\d{5}\\D+\\S*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MagasinQuantityRegex = new(
        "(^|\\s)(-?\\d+(?:[.,]\\d+)?)\\s+-?\\d+(?:[.,]\\d+)?\\s+-?\\d+(?:[.,]\\d+)?\\s+-?\\d+(?:[.,]\\d+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly ParserDefinition[] ParserDefinitions =
    [
        new(
            StructuredOrderDocumentRule.ArtnrBaseQuantity,
            pageText => pageText.Contains("Artnr", StringComparison.Ordinal) &&
                        pageText.Contains("Ant/bas", StringComparison.Ordinal),
            ParseBaseQuantityPage,
            "Artnr / Ant/bas parser",
            "Matchade ett läsbart PDF-textlager med Artnr- och Ant/bas-kolumner."),
        new(
            StructuredOrderDocumentRule.SupplierArticleQuantity,
            pageText => pageText.Contains("Ert Art. Nr", StringComparison.Ordinal) &&
                        pageText.Contains("Antal (pcs)", StringComparison.Ordinal),
            ParseSupplierArticleQuantityPage,
            "Supplier item / Antal (pcs) parser",
            "Matchade en leverantörstabell med Ert Art. Nr och Antal (pcs)-kolumner."),
        new(
            StructuredOrderDocumentRule.SupplierLevArtQuantity,
            pageText => pageText.Contains("Lev. art.nr", StringComparison.Ordinal) &&
                        pageText.Contains("Antal", StringComparison.Ordinal) &&
                        pageText.Contains("Belopp", StringComparison.Ordinal),
            ParseTuraPage,
            "Lev. art.nr / Antal parser",
            "Matchade en läsbar inköpsordertabell och använde Lev. art.nr som leverantörens artikelnummer i stället för köparens Artikelnr-kolumn."),
        new(
            StructuredOrderDocumentRule.BenamningLevArtQuantity,
            pageText => pageText.Contains("Benämning", StringComparison.Ordinal) &&
                        pageText.Contains("Antal", StringComparison.Ordinal) &&
                        pageText.Contains("Lev. Art. Nr.", StringComparison.Ordinal) &&
                        pageText.Contains("Lagerplats", StringComparison.Ordinal),
            ParseBenamningPage,
            "Benämning / Lev. Art. Nr. parser",
            "Matchade en läsbar inköpsordertabell och använde Lev. Art. Nr. med Antal-kolumnen, samtidigt som Lagerplats ignorerades."),
        new(
            StructuredOrderDocumentRule.SallingHolditQuantity,
            pageText => pageText.Contains("PO Quantity", StringComparison.Ordinal) &&
                        pageText.Contains("Pcs/PO UoM", StringComparison.Ordinal) &&
                        pageText.Contains("Vendor Art Code", StringComparison.Ordinal),
            ParseSallingPage,
            "PO Quantity / Pcs/PO UoM parser",
            "Matchade en läsbar inköpsordertabell och använde leverantörens artikelnummer efter HOLD IT i artikelbeskrivningen, medan köparens artikelnummer och den duplicerade EAN-raden ignorerades."),
        new(
            StructuredOrderDocumentRule.ManufacturingPartNumberQuantity,
            pageText => pageText.Contains("Quantity UoM", StringComparison.Ordinal) &&
                        pageText.Contains("Manufacturing Part Number", StringComparison.Ordinal) &&
                        pageText.Contains("Material", StringComparison.Ordinal),
            ParseManufacturingPartNumberPage,
            "Quantity UoM / Manufacturing Part Number parser",
            "Matchade en läsbar inköpsordertabell och använde ordered quantity från Quantity UoM samt leverantörens artikelnummer från Manufacturing Part Number-raden, medan Material-kolumnen ignorerades."),
        new(
            StructuredOrderDocumentRule.MagasinArticleQuantity,
            pageText => pageText.Contains("Dessin", StringComparison.Ordinal) &&
                        pageText.Contains("Quantity", StringComparison.Ordinal) &&
                        pageText.Contains("Amount", StringComparison.Ordinal),
            ParseMagasinPage,
            "Dessin / Quantity parser",
            "Matchade en läsbar inköpsordertabell med Dessin-rader och Quantity-kolumn, utan att ta med duplicerade EAN-rader.")
    ];

    private readonly IOptions<FlowEngineModuleOptions> _options;
    private readonly ILogger<FlowEngineOrderDocumentExtractionService> _logger;

    public FlowEngineOrderDocumentExtractionService(
        IOptions<FlowEngineModuleOptions> options,
        ILogger<FlowEngineOrderDocumentExtractionService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<FlowEngineOrderDocumentExtractionResult> ExtractAsync(
        FlowEngineOrderDocumentInput document,
        CancellationToken cancellationToken = default)
    {
        var fileName = (document.FileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidOperationException("Dokumentets filnamn saknas.");

        if (document.Data is null || document.Data.Length == 0)
            throw new InvalidOperationException("Det uppladdade dokumentet är tomt.");

        var maxBytes = Math.Max(1, _options.Value.DocumentExtractMaxBytes);
        if (document.Data.Length > maxBytes)
            throw new InvalidOperationException($"Det uppladdade dokumentet är för stort. Maxstorlek är {maxBytes} byte.");

        if (!string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Dokumenttypen för '{fileName}' stöds inte. Endast PDF tillåts.");

        var pageTexts = await ExtractPdfPageTextsAsync(document.Data, cancellationToken);
        if (pageTexts.Count == 0)
        {
            throw new InvalidOperationException(
                "PDF-dokumentet kunde inte läsas med portalens inbyggda .NET-parser. Säkerställ att filen har ett läsbart textlager.");
        }

        var parsed = ParsePurchaseOrderPages(pageTexts);
        if (parsed is null)
        {
            throw new InvalidOperationException(
                "Ingen matchande deterministisk PDF-parser hittades för dokumentet. Flagga det så en ny parser kan läggas till.");
        }

        var normalizedLines = NormalizeExtractedLines(parsed.Lines);
        return new FlowEngineOrderDocumentExtractionResult
        {
            Lines = normalizedLines,
            Source = parsed.Source
        };
    }

    private static IReadOnlyList<FlowEngineOrderDocumentExtractionLine> NormalizeExtractedLines(
        IReadOnlyList<FlowEngineOrderDocumentExtractionLine> rawLines)
    {
        var normalized = new List<FlowEngineOrderDocumentExtractionLine>();
        var invalidLineCount = 0;

        foreach (var line in rawLines)
        {
            var articleNumber = (line.ArticleNumber ?? string.Empty).Trim();
            var quantity = (line.Quantity ?? string.Empty).Trim();

            if (!IsValidArticleNumber(articleNumber) || NormalizeQuantityString(quantity) is not { } normalizedQuantity)
            {
                invalidLineCount++;
                continue;
            }

            normalized.Add(new FlowEngineOrderDocumentExtractionLine
            {
                ArticleNumber = articleNumber,
                Quantity = normalizedQuantity
            });
        }

        if (normalized.Count == 0)
            throw new InvalidOperationException("Dokumentet gav inga giltiga orderrader.");

        if (invalidLineCount > 0)
            throw new InvalidOperationException("Dokumentextraktionen gav ogiltiga rader och stoppades.");

        return normalized;
    }

    private static bool IsValidArticleNumber(string value)
        => !string.IsNullOrWhiteSpace(value) && ValidArticleNumberRegex.IsMatch(value);

    private static string? NormalizeQuantityString(string value)
    {
        var normalized = NormalizeDecimalString(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
            return null;

        if (decimalValue == 0)
            return null;

        return decimalValue.ToString("0.#############################", CultureInfo.InvariantCulture);
    }

    private static string NormalizeDecimalString(string value)
    {
        var candidate = (value ?? string.Empty)
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\u2212", "-", StringComparison.Ordinal)
            .Replace("\u2012", "-", StringComparison.Ordinal)
            .Replace("\u2013", "-", StringComparison.Ordinal)
            .Replace("\u2014", "-", StringComparison.Ordinal)
            .Replace("\u2015", "-", StringComparison.Ordinal)
            .Replace("\uFE63", "-", StringComparison.Ordinal)
            .Replace("\uFF0D", "-", StringComparison.Ordinal);

        var hasComma = candidate.Contains(',', StringComparison.Ordinal);
        var hasDot = candidate.Contains('.', StringComparison.Ordinal);

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

    private static ParsedDocumentResult? ParsePurchaseOrderPages(IReadOnlyList<string> pageTexts)
    {
        var extracted = new List<FlowEngineOrderDocumentExtractionLine>();
        var matchedRules = new HashSet<StructuredOrderDocumentRule>();

        foreach (var pageText in pageTexts)
        {
            var lines = pageText
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            var definition = ParserDefinitions.FirstOrDefault(candidate => candidate.Matches(pageText));
            if (definition is null)
                continue;

            var pageLines = definition.Parse(lines);
            if (pageLines.Count == 0)
                continue;

            matchedRules.Add(definition.Rule);
            extracted.AddRange(pageLines);
        }

        if (extracted.Count == 0)
            return null;

        return new ParsedDocumentResult
        {
            Lines = extracted,
            Source = SummarizeSource(matchedRules)
        };
    }

    private static List<FlowEngineOrderDocumentExtractionLine> ParseBaseQuantityPage(IReadOnlyList<string> lines)
    {
        var extracted = new List<FlowEngineOrderDocumentExtractionLine>();
        var pendingArticleNumbers = new Queue<string>();

        foreach (var line in lines)
        {
            if (ParseArticleNumber(line) is { } articleNumber)
                pendingArticleNumbers.Enqueue(articleNumber);

            if (ParseBaseQuantity(line) is { } quantity && pendingArticleNumbers.Count > 0)
            {
                extracted.Add(new FlowEngineOrderDocumentExtractionLine
                {
                    ArticleNumber = pendingArticleNumbers.Dequeue(),
                    Quantity = quantity
                });
            }
        }

        return extracted;
    }

    private static List<FlowEngineOrderDocumentExtractionLine> ParseSupplierArticleQuantityPage(IReadOnlyList<string> lines)
    {
        var supplierArticleNumbers = new Queue<string>();
        var quantities = new Queue<string>();
        var extracted = new List<FlowEngineOrderDocumentExtractionLine>();
        var isInsideSupplierTable = false;

        foreach (var line in lines)
        {
            if (!isInsideSupplierTable)
            {
                isInsideSupplierTable = line.Contains("Ert Art. Nr", StringComparison.Ordinal);
                continue;
            }

            if (line.StartsWith("Valuta", StringComparison.Ordinal) ||
                line.StartsWith("Clas Ohlson AB", StringComparison.Ordinal) ||
                line.StartsWith("Telefon", StringComparison.Ordinal))
            {
                break;
            }

            if (ParseSupplierArticleNumber(line) is { } articleNumber)
                supplierArticleNumbers.Enqueue(articleNumber);

            if (ParseQuantityWithDeliveryDate(line) is { } quantity)
                quantities.Enqueue(quantity);

            while (supplierArticleNumbers.Count > 0 && quantities.Count > 0)
            {
                extracted.Add(new FlowEngineOrderDocumentExtractionLine
                {
                    ArticleNumber = supplierArticleNumbers.Dequeue(),
                    Quantity = quantities.Dequeue()
                });
            }
        }

        if (supplierArticleNumbers.Count > 0 || quantities.Count > 0)
            return new List<FlowEngineOrderDocumentExtractionLine>();

        return extracted;
    }

    private static string? ParseArticleNumber(string line)
    {
        var match = BaseArticleNumberRegex.Match(line ?? string.Empty);
        return match.Success ? match.Value : null;
    }

    private static string? ParseBaseQuantity(string line)
    {
        var match = BaseQuantityRegex.Match(line ?? string.Empty);
        if (!match.Success || match.Groups.Count < 2)
            return null;

        var quantity = match.Groups[1].Value.Trim().Replace(",", ".", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(quantity) ? null : quantity;
    }

    private static string? ParseSupplierArticleNumber(string line)
    {
        var candidate = (line ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(candidate) ||
            candidate.Length < 5 ||
            candidate.EndsWith("/", StringComparison.Ordinal) ||
            IsDeliveryDateToken(candidate) ||
            IsDecimalAmountToken(candidate) ||
            !ValidArticleNumberRegex.IsMatch(candidate) ||
            !candidate.Any(char.IsDigit))
        {
            return null;
        }

        return candidate;
    }

    private static List<FlowEngineOrderDocumentExtractionLine> ParseTuraPage(IReadOnlyList<string> lines)
    {
        var extracted = new List<FlowEngineOrderDocumentExtractionLine>();
        var isInsideTable = false;

        foreach (var line in lines)
        {
            if (!isInsideTable)
            {
                isInsideTable = line.Contains("Lev. art.nr", StringComparison.Ordinal) &&
                                line.Contains("Antal", StringComparison.Ordinal) &&
                                line.Contains("Belopp", StringComparison.Ordinal);
                continue;
            }

            if (line.StartsWith("Vänligen bekräfta", StringComparison.Ordinal) ||
                line.StartsWith("Leveransvillkor", StringComparison.Ordinal) ||
                line.StartsWith("Energigatan", StringComparison.Ordinal))
            {
                break;
            }

            var row = ParseTuraInlineRow(line) ?? ParseTuraSplitRow(line);
            if (row is not null)
                extracted.Add(row);
        }

        return extracted;
    }

    private static FlowEngineOrderDocumentExtractionLine? ParseTuraInlineRow(string line)
    {
        var tokens = (line ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length < 7 ||
            !IsIntegerToken(tokens[0]) ||
            !IsArticleToken(tokens[1]) ||
            !IsArticleToken(tokens[2]) ||
            !IsNumericToken(tokens[3]) ||
            !IsCurrencyToken(tokens[4]) ||
            !IsNumericToken(tokens[5]) ||
            !IsIsoDeliveryDateToken(tokens[6]))
        {
            return null;
        }

        return new FlowEngineOrderDocumentExtractionLine
        {
            ArticleNumber = tokens[2],
            Quantity = NormalizeInlineQuantity(tokens[5])
        };
    }

    private static FlowEngineOrderDocumentExtractionLine? ParseTuraSplitRow(string line)
    {
        var tokens = (line ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length != 4 ||
            !IsArticleToken(tokens[0]) ||
            !IsArticleToken(tokens[1]) ||
            !IsNumericToken(tokens[2]) ||
            !IsIsoDeliveryDateToken(tokens[3]))
        {
            return null;
        }

        return new FlowEngineOrderDocumentExtractionLine
        {
            ArticleNumber = tokens[1],
            Quantity = NormalizeInlineQuantity(tokens[2])
        };
    }

    private static List<FlowEngineOrderDocumentExtractionLine> ParseBenamningPage(IReadOnlyList<string> lines)
    {
        var extracted = new List<FlowEngineOrderDocumentExtractionLine>();
        var isInsideTable = false;

        foreach (var line in lines)
        {
            if (!isInsideTable)
            {
                isInsideTable = line.Contains("Benämning", StringComparison.Ordinal) &&
                                line.Contains("Lev. Art. Nr.", StringComparison.Ordinal);
                continue;
            }

            if (line.StartsWith("Bankgiro", StringComparison.Ordinal) ||
                line.StartsWith("Plusgiro", StringComparison.Ordinal) ||
                line.StartsWith("Organisationsnr", StringComparison.Ordinal) ||
                line.StartsWith("EU VAT nr", StringComparison.Ordinal))
            {
                break;
            }

            if (ParseBenamningRow(line) is { } row)
                extracted.Add(row);
        }

        return extracted;
    }

    private static FlowEngineOrderDocumentExtractionLine? ParseBenamningRow(string line)
    {
        var tokens = (line ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length < 4 ||
            !IsNumericToken(tokens[^3]) ||
            !IsArticleToken(tokens[^2]) ||
            !IsLocationToken(tokens[^1]))
        {
            return null;
        }

        return new FlowEngineOrderDocumentExtractionLine
        {
            ArticleNumber = tokens[^2],
            Quantity = NormalizeInlineQuantity(tokens[^3])
        };
    }

    private static List<FlowEngineOrderDocumentExtractionLine> ParseSallingPage(IReadOnlyList<string> lines)
    {
        var extracted = new List<FlowEngineOrderDocumentExtractionLine>();
        var isInsideTable = false;

        foreach (var line in lines)
        {
            if (!isInsideTable)
            {
                isInsideTable = line.Contains("PO Quantity", StringComparison.Ordinal) &&
                                line.Contains("Pcs/PO UoM", StringComparison.Ordinal);
                continue;
            }

            if (ParseSallingPrimaryRow(line) is { } row)
                extracted.Add(row);
        }

        return extracted;
    }

    private static FlowEngineOrderDocumentExtractionLine? ParseSallingPrimaryRow(string line)
    {
        var match = SallingPrimaryRowRegex.Match(line ?? string.Empty);
        if (!match.Success || match.Groups.Count < 3)
            return null;

        return new FlowEngineOrderDocumentExtractionLine
        {
            ArticleNumber = match.Groups[1].Value,
            Quantity = NormalizeInlineQuantity(match.Groups[2].Value)
        };
    }

    private static List<FlowEngineOrderDocumentExtractionLine> ParseManufacturingPartNumberPage(IReadOnlyList<string> lines)
    {
        var extracted = new List<FlowEngineOrderDocumentExtractionLine>();
        var isInsideTable = false;
        string? pendingQuantity = null;
        var isAwaitingArticleNumber = false;

        foreach (var line in lines)
        {
            if (!isInsideTable)
            {
                isInsideTable = line.Contains("Pos", StringComparison.Ordinal) &&
                                line.Contains("Quantity UoM", StringComparison.Ordinal) &&
                                line.Contains("Net Amount", StringComparison.Ordinal);
                continue;
            }

            if (line.Contains("Total net value excl. tax", StringComparison.Ordinal) ||
                line.StartsWith("Invoice Address", StringComparison.Ordinal) ||
                line.StartsWith("Delivery Address", StringComparison.Ordinal))
            {
                break;
            }

            if (ParseManufacturingQuantity(line) is { } quantity)
            {
                if (pendingQuantity is not null || isAwaitingArticleNumber)
                    return new List<FlowEngineOrderDocumentExtractionLine>();

                pendingQuantity = quantity;
                continue;
            }

            if (line.Contains("Manufacturing Part Number", StringComparison.Ordinal))
            {
                if (pendingQuantity is null || isAwaitingArticleNumber)
                    return new List<FlowEngineOrderDocumentExtractionLine>();

                isAwaitingArticleNumber = true;
                continue;
            }

            if (isAwaitingArticleNumber && ParseManufacturingArticleNumber(line) is { } articleNumber)
            {
                if (pendingQuantity is null)
                    return new List<FlowEngineOrderDocumentExtractionLine>();

                extracted.Add(new FlowEngineOrderDocumentExtractionLine
                {
                    ArticleNumber = articleNumber,
                    Quantity = pendingQuantity
                });
                pendingQuantity = null;
                isAwaitingArticleNumber = false;
            }
        }

        return pendingQuantity is null && !isAwaitingArticleNumber
            ? extracted
            : new List<FlowEngineOrderDocumentExtractionLine>();
    }

    private static string? ParseManufacturingQuantity(string line)
    {
        var match = ManufacturingQuantityRegex.Match(line ?? string.Empty);
        if (!match.Success || match.Groups.Count < 2)
            return null;

        return NormalizeInlineQuantity(match.Groups[1].Value);
    }

    private static string? ParseManufacturingArticleNumber(string line)
    {
        var trimmed = (line ?? string.Empty).Trim();
        return trimmed.Length >= 5 && trimmed.All(char.IsDigit)
            ? trimmed
            : null;
    }

    private static List<FlowEngineOrderDocumentExtractionLine> ParseMagasinPage(IReadOnlyList<string> lines)
    {
        var extracted = new List<FlowEngineOrderDocumentExtractionLine>();
        string? pendingArticleNumber = null;
        var isInsideTable = false;

        foreach (var line in lines)
        {
            if (!isInsideTable)
            {
                isInsideTable = line.Contains("Dessin", StringComparison.Ordinal) &&
                                line.Contains("Quantity", StringComparison.Ordinal) &&
                                line.Contains("Amount", StringComparison.Ordinal);
                continue;
            }

            if (line.StartsWith("Sales balance", StringComparison.Ordinal) ||
                line.StartsWith("A/S TH. WESSEL & VETT", StringComparison.Ordinal))
            {
                break;
            }

            if (ParseMagasinArticleNumber(line) is { } articleNumber)
            {
                if (ParseMagasinQuantity(line) is { } inlineQuantity)
                {
                    if (pendingArticleNumber is not null)
                        return new List<FlowEngineOrderDocumentExtractionLine>();

                    extracted.Add(new FlowEngineOrderDocumentExtractionLine
                    {
                        ArticleNumber = articleNumber,
                        Quantity = inlineQuantity
                    });
                }
                else
                {
                    if (pendingArticleNumber is not null)
                        return new List<FlowEngineOrderDocumentExtractionLine>();

                    pendingArticleNumber = articleNumber;
                }

                continue;
            }

            if (pendingArticleNumber is not null && ParseMagasinQuantity(line) is { } quantity)
            {
                extracted.Add(new FlowEngineOrderDocumentExtractionLine
                {
                    ArticleNumber = pendingArticleNumber,
                    Quantity = quantity
                });
                pendingArticleNumber = null;
            }
        }

        return pendingArticleNumber is null
            ? extracted
            : new List<FlowEngineOrderDocumentExtractionLine>();
    }

    private static string? ParseMagasinArticleNumber(string line)
    {
        var token = (line ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(token) || token.Length <= 5 || !MagasinArticleRegex.IsMatch(token))
            return null;

        return token[..5];
    }

    private static string? ParseMagasinQuantity(string line)
    {
        var match = MagasinQuantityRegex.Match(line ?? string.Empty);
        if (!match.Success || match.Groups.Count < 3)
            return null;

        return NormalizeInlineQuantity(match.Groups[2].Value);
    }

    private static string? ParseQuantityWithDeliveryDate(string line)
    {
        var tokens = (line ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var dateIndex = tokens.FindLastIndex(IsDeliveryDateToken);
        if (dateIndex < 3)
            return null;

        var tokenIndex = dateIndex - 1;
        if (!IsNumericToken(tokens[tokenIndex]))
            return null;

        tokenIndex--;
        tokenIndex = IndexAfterOptionalPercentToken(tokens, tokenIndex);
        if (tokenIndex < 1 || !IsNumericToken(tokens[tokenIndex]))
            return null;

        tokenIndex--;
        if (!IsNumericToken(tokens[tokenIndex]))
            return null;

        var quantity = tokens[tokenIndex].Trim().Replace(",", ".", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(quantity) ? null : quantity;
    }

    private static int IndexAfterOptionalPercentToken(IReadOnlyList<string> tokens, int index)
    {
        if (index < 0)
            return -1;

        var token = tokens[index];
        if (token == "%")
        {
            var discountIndex = index - 1;
            return discountIndex >= 0 && IsNumericToken(tokens[discountIndex])
                ? discountIndex - 1
                : -1;
        }

        if (token.EndsWith("%", StringComparison.Ordinal))
        {
            var numericPart = token[..^1];
            return IsNumericToken(numericPart)
                ? index - 1
                : -1;
        }

        return index;
    }

    private static bool IsDeliveryDateToken(string token)
        => DeliveryDateRegex.IsMatch(token ?? string.Empty);

    private static bool IsIsoDeliveryDateToken(string token)
        => IsoDeliveryDateRegex.IsMatch(token ?? string.Empty);

    private static bool IsNumericToken(string token)
        => NumericTokenRegex.IsMatch(token ?? string.Empty);

    private static bool IsDecimalAmountToken(string token)
        => DecimalAmountRegex.IsMatch(token ?? string.Empty);

    private static bool IsArticleToken(string token)
        => !string.IsNullOrWhiteSpace(token) &&
           token.Length >= 2 &&
           ValidArticleNumberRegex.IsMatch(token);

    private static bool IsCurrencyToken(string token)
        => CurrencyTokenRegex.IsMatch(token ?? string.Empty);

    private static bool IsIntegerToken(string token)
        => IntegerTokenRegex.IsMatch(token ?? string.Empty);

    private static bool IsLocationToken(string token)
        => LocationTokenRegex.IsMatch(token ?? string.Empty);

    private static string NormalizeInlineQuantity(string token)
        => (token ?? string.Empty).Replace(",", ".", StringComparison.Ordinal);

    private static FlowEngineOrderDocumentExtractionSource SummarizeSource(HashSet<StructuredOrderDocumentRule> matchedRules)
    {
        if (matchedRules.Count == 1)
        {
            var definition = ParserDefinitions.FirstOrDefault(candidate => candidate.Rule == matchedRules.First());
            if (definition is not null)
            {
                return new FlowEngineOrderDocumentExtractionSource
                {
                    Kind = "deterministic",
                    Label = definition.Label,
                    Detail = definition.Detail
                };
            }
        }

        return new FlowEngineOrderDocumentExtractionSource
        {
            Kind = "deterministic",
            Label = "Deterministic PDF parser",
            Detail = "Matchade flera läsbara köpordertabeller i dokumentet."
        };
    }

    private Task<IReadOnlyList<string>> ExtractPdfPageTextsAsync(byte[] data, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = new MemoryStream(data, writable: false);
            using var document = PdfDocument.Open(stream);
            var pages = new List<string>();

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pageText = ReconstructPageText(page);
                if (!string.IsNullOrWhiteSpace(pageText))
                    pages.Add(pageText);
            }

            return Task.FromResult<IReadOnlyList<string>>(pages);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "The built-in .NET PDF parser could not read the uploaded document.");
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }
    }

    private static string ReconstructPageText(Page page)
    {
        if (page.Letters.Count == 0)
            return page.Text?.Trim() ?? string.Empty;

        var sortedLetters = page.Letters
            .Where(letter => !string.IsNullOrEmpty(letter.Value))
            .OrderByDescending(letter => letter.Location.Y)
            .ThenBy(letter => letter.Location.X)
            .ToList();

        if (sortedLetters.Count == 0)
            return page.Text?.Trim() ?? string.Empty;

        var averageFontSize = sortedLetters
            .Select(letter => letter.FontSize)
            .Where(size => size > 0)
            .DefaultIfEmpty(10d)
            .Average();

        var lineTolerance = Math.Max(2d, averageFontSize * 0.35d);
        var lines = new List<LetterLine>();

        foreach (var letter in sortedLetters)
        {
            var matchingLine = lines.FirstOrDefault(line => Math.Abs(line.BaselineY - letter.Location.Y) <= lineTolerance);
            if (matchingLine is null)
            {
                matchingLine = new LetterLine(letter.Location.Y);
                lines.Add(matchingLine);
            }

            matchingLine.Letters.Add(letter);
        }

        var orderedLines = lines
            .OrderByDescending(line => line.BaselineY)
            .Select(BuildLineText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        return string.Join(Environment.NewLine, orderedLines);
    }

    private static string BuildLineText(LetterLine line)
    {
        var orderedLetters = line.Letters
            .OrderBy(letter => letter.Location.X)
            .ToList();

        if (orderedLetters.Count == 0)
            return string.Empty;

        var averageLetterWidth = orderedLetters
            .Select(letter => letter.Width)
            .Where(width => width > 0)
            .DefaultIfEmpty(5d)
            .Average();

        var builder = new System.Text.StringBuilder();
        Letter? previousLetter = null;

        foreach (var letter in orderedLetters)
        {
            if (previousLetter is not null)
            {
                var previousRightEdge = previousLetter.Location.X + previousLetter.Width;
                var gap = letter.Location.X - previousRightEdge;
                if (gap > averageLetterWidth * 0.6d)
                {
                    var spaces = Math.Clamp((int)Math.Round(gap / Math.Max(1d, averageLetterWidth), MidpointRounding.AwayFromZero), 1, 8);
                    builder.Append(' ', spaces);
                }
            }

            builder.Append(letter.Value);
            previousLetter = letter;
        }

        return builder.ToString().TrimEnd();
    }

    private sealed class ParsedDocumentResult
    {
        public List<FlowEngineOrderDocumentExtractionLine> Lines { get; set; } = new();
        public FlowEngineOrderDocumentExtractionSource Source { get; set; } = new();
    }

    private sealed record ParserDefinition(
        StructuredOrderDocumentRule Rule,
        Func<string, bool> Matches,
        Func<IReadOnlyList<string>, List<FlowEngineOrderDocumentExtractionLine>> Parse,
        string Label,
        string Detail);

    private sealed class LetterLine
    {
        public LetterLine(double baselineY)
        {
            BaselineY = baselineY;
        }

        public double BaselineY { get; }
        public List<Letter> Letters { get; } = new();
    }

    private enum StructuredOrderDocumentRule
    {
        ArtnrBaseQuantity,
        SupplierArticleQuantity,
        SupplierLevArtQuantity,
        BenamningLevArtQuantity,
        SallingHolditQuantity,
        ManufacturingPartNumberQuantity,
        MagasinArticleQuantity
    }
}

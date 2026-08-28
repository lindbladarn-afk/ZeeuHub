namespace WebApp.Services.ExcelImport;

// Reads simple Excel import workbooks using a module-provided header and row definition.
public interface IExcelImportWorkbookReader
{
    Task<ExcelImportWorkbookReadResult> ReadAsync(
        IFormFile file,
        ExcelImportWorkbookDefinition definition,
        CancellationToken cancellationToken = default);
}

public sealed class ExcelImportWorkbookDefinition
{
    public required IReadOnlyList<string> ExpectedHeaders { get; init; }
    public required Func<IReadOnlyList<string>, List<string>, bool> ValidateHeaders { get; init; }
    public required Func<IReadOnlyList<string>, IReadOnlyDictionary<string, int>, Dictionary<string, string>> BuildRowData { get; init; }
    public required Func<IReadOnlyDictionary<string, string>, bool> HasAnyValue { get; init; }
    public string EmptyFileError { get; init; } = "Filen innehåller inga rader att importera.";
}

public sealed class ExcelImportWorkbookReadResult
{
    public IReadOnlyList<string> RowHeaders { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ExcelImportWorkbookRow> Rows { get; init; } = Array.Empty<ExcelImportWorkbookRow>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed class ExcelImportWorkbookRow
{
    public int RowNo { get; init; }
    public Dictionary<string, string> Data { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

// Parses one supported import file format into the shared row model.
public interface IExcelImportWorkbookFileParser
{
    bool CanParse(string? extension);

    Task<ExcelImportWorkbookReadResult> ReadAsync(
        IFormFile file,
        ExcelImportWorkbookDefinition definition,
        CancellationToken cancellationToken = default);
}

// Selects the parser that matches the uploaded file format.
public sealed class ExcelImportWorkbookReader : IExcelImportWorkbookReader
{
    private readonly IReadOnlyList<IExcelImportWorkbookFileParser> _parsers;

    public ExcelImportWorkbookReader()
        : this(new IExcelImportWorkbookFileParser[]
        {
            new OpenXmlExcelImportWorkbookParser(),
            new LegacyXlsExcelImportWorkbookParser(),
            new CsvExcelImportWorkbookParser()
        })
    {
    }

    public ExcelImportWorkbookReader(IEnumerable<IExcelImportWorkbookFileParser> parsers)
    {
        _parsers = parsers.ToList();
    }

    public async Task<ExcelImportWorkbookReadResult> ReadAsync(
        IFormFile file,
        ExcelImportWorkbookDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(definition);

        if (file.Length <= 0)
        {
            return new ExcelImportWorkbookReadResult
            {
                RowHeaders = definition.ExpectedHeaders.ToList(),
                Errors = new[] { definition.EmptyFileError }
            };
        }

        if (file.Length > ExcelImportResourceLimits.MaxUploadBytes)
        {
            return new ExcelImportWorkbookReadResult
            {
                RowHeaders = definition.ExpectedHeaders.ToList(),
                Errors = new[] { ExcelImportResourceLimits.FileTooLargeMessage(ExcelImportResourceLimits.MaxUploadBytes) }
            };
        }

        var extension = Path.GetExtension(file.FileName);
        var parser = _parsers.FirstOrDefault(candidate => candidate.CanParse(extension));
        if (parser is null)
        {
            return new ExcelImportWorkbookReadResult
            {
                RowHeaders = definition.ExpectedHeaders.ToList(),
                Errors = new[] { "Filtypen stöds inte för den här importen." }
            };
        }

        return await parser.ReadAsync(file, definition, cancellationToken);
    }
}

// Shared helpers for import parsers that validate fixed headers and build row dictionaries.
public static class ExcelImportWorkbookParserHelpers
{
    public static bool ValidateFixedHeaders(
        IReadOnlyList<string> actualHeaders,
        IReadOnlyList<string> expectedHeaders,
        List<string> errors)
    {
        for (var i = 0; i < expectedHeaders.Count; i++)
        {
            var cellValue = i < actualHeaders.Count
                ? actualHeaders[i].Trim()
                : string.Empty;

            if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.Ordinal))
            {
                errors.Add(BuildHeaderMismatchError(i + 1, expectedHeaders[i], cellValue));
                return false;
            }
        }

        var extraHeaders = actualHeaders
            .Skip(expectedHeaders.Count)
            .Select(header => header.Trim())
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .ToList();

        if (extraHeaders.Any())
        {
            errors.Add($"Filen innehåller fler kolumnrubriker än mallen tillåter: {string.Join(", ", extraHeaders)}.");
            return false;
        }

        return true;
    }

    private static string BuildHeaderMismatchError(int columnNumber, string expectedHeader, string actualHeader)
    {
        var hint = ResolveTemplateHint(expectedHeader, actualHeader);
        var message = $"Fel mall för vald importtyp. Rubrik i kolumn {columnNumber} matchar inte: förväntat '{expectedHeader}', fick '{actualHeader}'.";
        return string.IsNullOrWhiteSpace(hint)
            ? $"{message} Kontrollera att du valt rätt importtyp och rätt mall."
            : $"{message} {hint}";
    }

    private static string ResolveTemplateHint(string expectedHeader, string actualHeader)
    {
        if (string.Equals(expectedHeader, "Artnr", StringComparison.OrdinalIgnoreCase)
            && string.Equals(actualHeader, "Företagkod", StringComparison.OrdinalIgnoreCase))
        {
            return "Filen verkar vara mallen för Inköpspriser. Byt importtyp till Inköpspriser och importera igen.";
        }

        if (string.Equals(expectedHeader, "Företagkod", StringComparison.OrdinalIgnoreCase)
            && string.Equals(actualHeader, "Artnr", StringComparison.OrdinalIgnoreCase))
        {
            return "Filen verkar vara mallen för Prisuppdatering. Byt importtyp till Prisuppdatering och importera igen.";
        }

        return string.Empty;
    }

    public static Dictionary<string, int> BuildExpectedHeaderMap(IReadOnlyList<string> expectedHeaders)
        => expectedHeaders
            .Select((name, index) => new { name, col = index + 1 })
            .ToDictionary(x => x.name, x => x.col, StringComparer.OrdinalIgnoreCase);

    public static Dictionary<string, string> BuildFixedRowData(
        IReadOnlyList<string> cells,
        IReadOnlyDictionary<string, int> headers,
        IReadOnlyList<string> expectedHeaders)
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in expectedHeaders)
        {
            if (!headers.TryGetValue(header, out var col))
            {
                data[header] = string.Empty;
                continue;
            }

            var index = col - 1;
            data[header] = index >= 0 && index < cells.Count
                ? cells[index].Trim()
                : string.Empty;
        }

        return data;
    }
}

using System.Text;

namespace WebApp.Services.ExcelImport;

// Reads CSV imports with strict limits and no formula or type evaluation.
public sealed class CsvExcelImportWorkbookParser : IExcelImportWorkbookFileParser
{
    private static readonly char[] CandidateDelimiters = { ';', ',', '\t' };

    public bool CanParse(string? extension)
        => string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase);

    public async Task<ExcelImportWorkbookReadResult> ReadAsync(
        IFormFile file,
        ExcelImportWorkbookDefinition definition,
        CancellationToken cancellationToken = default)
    {
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 16 * 1024, leaveOpen: false);

        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return new ExcelImportWorkbookReadResult
            {
                RowHeaders = definition.ExpectedHeaders.ToList(),
                Errors = new[] { definition.EmptyFileError }
            };
        }

        var delimiter = DetectDelimiter(headerLine, definition.ExpectedHeaders.Count);
        var headerCells = ParseLine(headerLine, delimiter);
        if (headerCells.Count > ExcelImportResourceLimits.MaxColumns)
        {
            return new ExcelImportWorkbookReadResult
            {
                RowHeaders = definition.ExpectedHeaders.ToList(),
                Errors = new[] { ExcelImportResourceLimits.TooManyColumnsMessage(ExcelImportResourceLimits.MaxColumns) }
            };
        }

        if (headerCells.Any(cell => cell.Length > ExcelImportResourceLimits.MaxCellLength))
        {
            return new ExcelImportWorkbookReadResult
            {
                RowHeaders = definition.ExpectedHeaders.ToList(),
                Errors = new[] { ExcelImportResourceLimits.CellTooLongMessage(1, ExcelImportResourceLimits.MaxCellLength) }
            };
        }

        var errors = new List<string>();
        if (!definition.ValidateHeaders(headerCells, errors) || errors.Any())
        {
            return new ExcelImportWorkbookReadResult
            {
                RowHeaders = definition.ExpectedHeaders.ToList(),
                Errors = errors
            };
        }

        var headers = ExcelImportWorkbookParserHelpers.BuildExpectedHeaderMap(definition.ExpectedHeaders);
        var rows = new List<ExcelImportWorkbookRow>();
        var rowNo = 1;
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            rowNo++;
            if (rowNo - 1 > ExcelImportResourceLimits.MaxStandardRows)
            {
                return new ExcelImportWorkbookReadResult
                {
                    RowHeaders = definition.ExpectedHeaders.ToList(),
                    Errors = new[] { ExcelImportResourceLimits.TooManyRowsMessage(ExcelImportResourceLimits.MaxStandardRows) }
                };
            }

            var cells = ParseLine(line, delimiter);
            if (cells.Count > ExcelImportResourceLimits.MaxColumns)
            {
                return new ExcelImportWorkbookReadResult
                {
                    RowHeaders = definition.ExpectedHeaders.ToList(),
                    Errors = new[] { ExcelImportResourceLimits.TooManyColumnsMessage(ExcelImportResourceLimits.MaxColumns) }
                };
            }

            if (cells.Any(cell => cell.Length > ExcelImportResourceLimits.MaxCellLength))
            {
                return new ExcelImportWorkbookReadResult
                {
                    RowHeaders = definition.ExpectedHeaders.ToList(),
                    Errors = new[] { ExcelImportResourceLimits.CellTooLongMessage(rowNo, ExcelImportResourceLimits.MaxCellLength) }
                };
            }

            var rowData = definition.BuildRowData(cells, headers);
            if (!definition.HasAnyValue(rowData))
                continue;

            rows.Add(new ExcelImportWorkbookRow
            {
                RowNo = rowNo,
                Data = rowData
            });
        }

        return new ExcelImportWorkbookReadResult
        {
            RowHeaders = definition.ExpectedHeaders.ToList(),
            Rows = rows
        };
    }

    private static char DetectDelimiter(string headerLine, int expectedColumns)
    {
        return CandidateDelimiters
            .Select(delimiter => new { Delimiter = delimiter, Count = ParseLine(headerLine, delimiter).Count })
            .OrderByDescending(candidate => candidate.Count == expectedColumns)
            .ThenBy(candidate => Math.Abs(candidate.Count - expectedColumns))
            .First()
            .Delimiter;
    }

    private static List<string> ParseLine(string line, char delimiter)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var current = line[index];
            if (current == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    cell.Append('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (current == delimiter && !inQuotes)
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
                continue;
            }

            cell.Append(current);
        }

        cells.Add(cell.ToString().Trim());
        return cells;
    }
}

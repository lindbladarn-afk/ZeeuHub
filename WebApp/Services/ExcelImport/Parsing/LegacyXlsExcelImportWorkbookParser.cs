using System.Globalization;
using System.Text;
using ExcelDataReader;

namespace WebApp.Services.ExcelImport;

// Reads legacy BIFF .xls files without macros, formula evaluation, or persistent storage.
public sealed class LegacyXlsExcelImportWorkbookParser : IExcelImportWorkbookFileParser
{
    static LegacyXlsExcelImportWorkbookParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public bool CanParse(string? extension)
        => string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase);

    public Task<ExcelImportWorkbookReadResult> ReadAsync(
        IFormFile file,
        ExcelImportWorkbookDefinition definition,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var stream = file.OpenReadStream();
            using var reader = ExcelReaderFactory.CreateBinaryReader(stream, new ExcelReaderConfiguration
            {
                FallbackEncoding = Encoding.GetEncoding(1252),
                LeaveOpen = false
            });

            return Task.FromResult(ReadFirstWorksheet(reader, definition, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Task.FromResult(new ExcelImportWorkbookReadResult
            {
                RowHeaders = definition.ExpectedHeaders.ToList(),
                Errors = new[] { "Filen kunde inte läsas. Kontrollera att den är en giltig .xls-fil." }
            });
        }
    }

    private static ExcelImportWorkbookReadResult ReadFirstWorksheet(
        IExcelDataReader reader,
        ExcelImportWorkbookDefinition definition,
        CancellationToken cancellationToken)
    {
        var headerRowNo = 0;
        IReadOnlyList<string>? headerCells = null;

        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            headerRowNo++;
            var cells = ReadCells(reader, definition.ExpectedHeaders.Count);
            if (cells.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            {
                headerCells = cells;
                break;
            }
        }

        if (headerCells is null)
        {
            return new ExcelImportWorkbookReadResult
            {
                RowHeaders = definition.ExpectedHeaders.ToList(),
                Errors = new[] { definition.EmptyFileError }
            };
        }

        if (reader.FieldCount > ExcelImportResourceLimits.MaxColumns)
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
                Errors = new[] { ExcelImportResourceLimits.CellTooLongMessage(headerRowNo, ExcelImportResourceLimits.MaxCellLength) }
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
        var rowNo = headerRowNo;
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNo++;
            if (rowNo - headerRowNo > ExcelImportResourceLimits.MaxStandardRows)
            {
                return new ExcelImportWorkbookReadResult
                {
                    RowHeaders = definition.ExpectedHeaders.ToList(),
                    Errors = new[] { ExcelImportResourceLimits.TooManyRowsMessage(ExcelImportResourceLimits.MaxStandardRows) }
                };
            }

            if (reader.FieldCount > ExcelImportResourceLimits.MaxColumns)
            {
                return new ExcelImportWorkbookReadResult
                {
                    RowHeaders = definition.ExpectedHeaders.ToList(),
                    Errors = new[] { ExcelImportResourceLimits.TooManyColumnsMessage(ExcelImportResourceLimits.MaxColumns) }
                };
            }

            var cells = ReadCells(reader, definition.ExpectedHeaders.Count);
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

    private static List<string> ReadCells(IExcelDataReader reader, int minimumColumns)
    {
        var columnCount = Math.Max(reader.FieldCount, minimumColumns);
        var cells = new List<string>(columnCount);

        for (var column = 0; column < columnCount; column++)
        {
            var value = reader.GetValue(column);
            cells.Add(ToSafeCellText(value));
        }

        return cells;
    }

    private static string ToSafeCellText(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
            _ => value.ToString()?.Trim() ?? string.Empty
        };
    }
}

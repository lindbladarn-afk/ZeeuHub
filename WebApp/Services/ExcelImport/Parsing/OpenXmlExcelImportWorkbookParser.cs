using System.Globalization;
using ExcelDataReader;

namespace WebApp.Services.ExcelImport;

// Streams modern OpenXML workbooks into the shared Excel Import row model.
public sealed class OpenXmlExcelImportWorkbookParser : IExcelImportWorkbookFileParser
{
    public bool CanParse(string? extension)
        => string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
           || string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase);

    public async Task<ExcelImportWorkbookReadResult> ReadAsync(
        IFormFile file,
        ExcelImportWorkbookDefinition definition,
        CancellationToken cancellationToken = default)
    {
        await using var input = file.OpenReadStream();
        MemoryStream? bufferedInput = null;
        Stream seekableInput = input;

        try
        {
            if (!input.CanSeek)
            {
                bufferedInput = new MemoryStream();
                await input.CopyToAsync(bufferedInput, cancellationToken);
                seekableInput = bufferedInput;
            }

            if (!ExcelImportOpenXmlPackageValidator.TryValidate(seekableInput, out var packageError))
                return CreateFailure(definition, packageError);

            seekableInput.Position = 0;
            using var reader = ExcelReaderFactory.CreateOpenXmlReader(seekableInput, new ExcelReaderConfiguration
            {
                LeaveOpen = true
            });

            return ReadFirstWorksheet(reader, definition, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return CreateFailure(definition, "Filen kunde inte läsas. Kontrollera att den är en giltig Excel-fil.");
        }
        finally
        {
            if (bufferedInput is not null)
                await bufferedInput.DisposeAsync();
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

            if (reader.FieldCount > ExcelImportResourceLimits.MaxColumns)
                return CreateFailure(definition, ExcelImportResourceLimits.TooManyColumnsMessage(ExcelImportResourceLimits.MaxColumns));

            var cells = ReadCells(reader, definition.ExpectedHeaders.Count);
            if (cells.Any(cell => cell.Length > ExcelImportResourceLimits.MaxCellLength))
                return CreateFailure(definition, ExcelImportResourceLimits.CellTooLongMessage(headerRowNo, ExcelImportResourceLimits.MaxCellLength));

            if (cells.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            {
                headerCells = cells;
                break;
            }
        }

        if (headerCells is null)
            return CreateFailure(definition, definition.EmptyFileError);

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
                return CreateFailure(definition, ExcelImportResourceLimits.TooManyRowsMessage(ExcelImportResourceLimits.MaxStandardRows));
            if (reader.FieldCount > ExcelImportResourceLimits.MaxColumns)
                return CreateFailure(definition, ExcelImportResourceLimits.TooManyColumnsMessage(ExcelImportResourceLimits.MaxColumns));

            var cells = ReadCells(reader, definition.ExpectedHeaders.Count);
            if (cells.Any(cell => cell.Length > ExcelImportResourceLimits.MaxCellLength))
                return CreateFailure(definition, ExcelImportResourceLimits.CellTooLongMessage(rowNo, ExcelImportResourceLimits.MaxCellLength));

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
            cells.Add(ToSafeCellText(reader.GetValue(column)));

        return cells;
    }

    private static string ToSafeCellText(object? value)
        => value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
            _ => value.ToString()?.Trim() ?? string.Empty
        };

    private static ExcelImportWorkbookReadResult CreateFailure(
        ExcelImportWorkbookDefinition definition,
        string error)
        => new()
        {
            RowHeaders = definition.ExpectedHeaders.ToList(),
            Errors = new[] { error }
        };
}

namespace WebApp.Services.ExcelImport;

// Runs the shared validation and all-or-nothing staging flow for editable import templates.
public interface IExcelImportEditSessionEngine
{
    Task<ExcelImportResult> CreateFromFileAsync<TStagingRow>(
        IFormFile file,
        ExcelImportEditTemplate<TStagingRow> template,
        int maxRows,
        CancellationToken cancellationToken = default);

    ExcelImportResult CreateEmpty<TStagingRow>(ExcelImportEditTemplate<TStagingRow> template);

    Task<ExcelImportResult> ImportEditedRowsAsync<TStagingRow>(
        Guid editSessionId,
        IReadOnlyList<ExcelImportEditableRow> rows,
        string importedBy,
        ExcelImportEditTemplate<TStagingRow> template,
        Func<IReadOnlyCollection<TStagingRow>, CancellationToken, Task> stageRowsAsync,
        CancellationToken cancellationToken = default);
}

public sealed class ExcelImportEditTemplate<TStagingRow>
{
    public required string ImportType { get; init; }
    public required ExcelImportWorkbookDefinition WorkbookDefinition { get; init; }
    public required IReadOnlyList<string> Headers { get; init; }
    public required Func<IReadOnlyDictionary<string, string>, IEnumerable<string>> ValidateRow { get; init; }
    public required Func<IReadOnlyDictionary<string, string>, string> BuildRowSnapshot { get; init; }
    public required Func<IReadOnlyDictionary<string, string>, bool> HasAnyValue { get; init; }
    public required Func<string, string, string> NormalizeValue { get; init; }
    public required Func<ExcelImportStagingRowContext, TStagingRow> CreateStagingRow { get; init; }
}

public sealed class ExcelImportEditableRow
{
    public int RowNo { get; init; }
    public IReadOnlyDictionary<string, string> Data { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class ExcelImportEditSessionEngine : IExcelImportEditSessionEngine
{
    private const int MaxValidationErrors = 200;
    private const int MaxSnapshotLength = 1_000;

    private readonly IExcelImportContextService _contextService;
    private readonly IExcelImportWorkbookReader _workbookReader;
    private readonly IExcelImportResultFactory _resultFactory;

    public ExcelImportEditSessionEngine(
        IExcelImportContextService contextService,
        IExcelImportWorkbookReader workbookReader,
        IExcelImportResultFactory resultFactory)
    {
        _contextService = contextService;
        _workbookReader = workbookReader;
        _resultFactory = resultFactory;
    }

    public async Task<ExcelImportResult> CreateFromFileAsync<TStagingRow>(
        IFormFile file,
        ExcelImportEditTemplate<TStagingRow> template,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRows);

        var workbook = await _workbookReader.ReadAsync(
            file,
            template.WorkbookDefinition,
            cancellationToken);
        if (workbook.Errors.Count > 0)
        {
            return CreateResult(
                template.ImportType,
                Guid.NewGuid(),
                null,
                workbook.RowHeaders,
                errors: workbook.Errors);
        }

        if (workbook.Rows.Count > maxRows)
            throw new EditableImportRowLimitExceededException(workbook.Rows.Count, maxRows);

        var editSessionId = Guid.NewGuid();
        return ValidateRows(
            template,
            editSessionId,
            editSessionId,
            workbook.RowHeaders,
            workbook.Rows.Select(row => new ExcelImportEditableRow
            {
                RowNo = row.RowNo,
                Data = row.Data
            }).ToList(),
            cancellationToken);
    }

    public ExcelImportResult CreateEmpty<TStagingRow>(ExcelImportEditTemplate<TStagingRow> template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var editSessionId = Guid.NewGuid();
        var emptyData = template.Headers.ToDictionary(
            header => header,
            _ => string.Empty,
            StringComparer.OrdinalIgnoreCase);

        return CreateResult(
            template.ImportType,
            editSessionId,
            editSessionId,
            template.Headers,
            rowResults:
            [
                new ExcelImportRowResult
                {
                    RowNo = 1,
                    IsValid = false,
                    Data = emptyData
                }
            ]);
    }

    public async Task<ExcelImportResult> ImportEditedRowsAsync<TStagingRow>(
        Guid editSessionId,
        IReadOnlyList<ExcelImportEditableRow> rows,
        string importedBy,
        ExcelImportEditTemplate<TStagingRow> template,
        Func<IReadOnlyCollection<TStagingRow>, CancellationToken, Task> stageRowsAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentException.ThrowIfNullOrWhiteSpace(importedBy);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(stageRowsAsync);

        var userContext = ExcelImportContextGuard.GetRequiredCurrent(_contextService);
        var batchId = Guid.NewGuid();
        var normalizedRows = rows
            .Select(row => new ExcelImportEditableRow
            {
                RowNo = row.RowNo,
                Data = NormalizeRowData(row.Data, template)
            })
            .Where(row => template.HasAnyValue(row.Data))
            .ToList();

        if (normalizedRows.Count == 0)
        {
            return CreateResult(
                template.ImportType,
                batchId,
                editSessionId,
                template.Headers,
                errors: ["Minst en rad måste innehålla data innan import."]);
        }

        var validationResult = ValidateRows(
            template,
            batchId,
            editSessionId,
            template.Headers,
            normalizedRows,
            cancellationToken);
        if (validationResult.Errors.Count > 0)
            return validationResult;

        var stagingRows = new List<TStagingRow>(normalizedRows.Count);
        for (var index = 0; index < normalizedRows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = normalizedRows[index];
            var rowNo = row.RowNo > 0 ? row.RowNo : index + 1;
            stagingRows.Add(template.CreateStagingRow(new ExcelImportStagingRowContext
            {
                ImportBatchId = batchId,
                RowNo = rowNo,
                RowData = row.Data,
                NonEmptyRowData = BuildNonEmptyRowData(row.Data),
                ImportedBy = importedBy,
                UserContext = userContext
            }));
        }

        await stageRowsAsync(stagingRows, cancellationToken);
        validationResult.StagedRows = stagingRows.Count;
        validationResult.EditSessionId = null;
        return validationResult;
    }

    private ExcelImportResult ValidateRows<TStagingRow>(
        ExcelImportEditTemplate<TStagingRow> template,
        Guid batchId,
        Guid? editSessionId,
        IReadOnlyList<string> headers,
        IReadOnlyList<ExcelImportEditableRow> rows,
        CancellationToken cancellationToken = default)
    {
        var rowResults = new List<ExcelImportRowResult>(rows.Count);
        var errors = new List<string>();
        var invalidRows = 0;

        for (var index = 0; index < rows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = rows[index];
            var rowNo = row.RowNo > 0 ? row.RowNo : index + 1;
            var hasOversizedCell = row.Data.Values.Any(value => value.Length > ExcelImportResourceLimits.MaxCellLength);
            var rowErrors = hasOversizedCell
                ? new List<string>
                {
                    $"En cell är för lång. Max {ExcelImportResourceLimits.MaxCellLength} tecken per cell."
                }
                : template.ValidateRow(row.Data).ToList();
            var isValid = rowErrors.Count == 0;
            if (!isValid)
            {
                invalidRows++;
                var snapshot = hasOversizedCell
                    ? "<raddata utelämnad>"
                    : template.BuildRowSnapshot(row.Data);
                AddValidationErrors(errors, rowErrors, rowNo, snapshot);
            }

            rowResults.Add(new ExcelImportRowResult
            {
                RowNo = rowNo,
                IsValid = isValid,
                ErrorMessage = isValid ? null : string.Join(" ", rowErrors),
                Data = row.Data.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase)
            });
        }

        return CreateResult(
            template.ImportType,
            batchId,
            editSessionId,
            headers,
            rows.Count,
            rows.Count - invalidRows,
            invalidRows,
            rowResults,
            errors);
    }

    private ExcelImportResult CreateResult(
        string importType,
        Guid batchId,
        Guid? editSessionId,
        IReadOnlyList<string> headers,
        int totalRows = 0,
        int validRows = 0,
        int invalidRows = 0,
        IReadOnlyList<ExcelImportRowResult>? rowResults = null,
        IReadOnlyList<string>? errors = null)
        => _resultFactory.Create(new ExcelImportResultCreateRequest
        {
            ImportType = importType,
            ImportBatchId = batchId,
            EditSessionId = editSessionId,
            TotalRows = totalRows,
            ValidRows = validRows,
            InvalidRows = invalidRows,
            RowHeaders = headers,
            RowResults = rowResults ?? [],
            Errors = errors ?? []
        });

    private static Dictionary<string, string> NormalizeRowData<TStagingRow>(
        IReadOnlyDictionary<string, string>? data,
        ExcelImportEditTemplate<TStagingRow> template)
    {
        data ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in template.Headers)
        {
            data.TryGetValue(header, out var value);
            normalized[header] = template.NormalizeValue(header, value ?? string.Empty);
        }

        return normalized;
    }

    private static Dictionary<string, string> BuildNonEmptyRowData(
        IReadOnlyDictionary<string, string> rowData)
        => rowData
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(
                item => item.Key,
                item => item.Value.Trim(),
                StringComparer.OrdinalIgnoreCase);

    private static void AddValidationErrors(
        List<string> target,
        IReadOnlyList<string> rowErrors,
        int rowNo,
        string snapshot)
    {
        var safeSnapshot = snapshot.Length <= MaxSnapshotLength
            ? snapshot
            : snapshot[..MaxSnapshotLength];
        foreach (var error in rowErrors)
        {
            if (target.Count >= MaxValidationErrors)
                return;
            target.Add($"Rad {rowNo}: {error} | Raddata: {safeSnapshot}");
        }
    }
}

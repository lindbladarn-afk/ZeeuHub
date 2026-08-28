namespace WebApp.Services.ExcelImport;

// Executes fixed-header imports while templates retain validation and staging mapping ownership.
public interface IExcelImportFixedTemplateEngine
{
    Task<ExcelImportResult> ImportAsync<TStagingRow>(
        IFormFile file,
        string importedBy,
        ExcelImportFixedTemplate<TStagingRow> template,
        Func<IReadOnlyCollection<TStagingRow>, CancellationToken, Task> stageRowsAsync,
        CancellationToken cancellationToken = default);
}

public sealed class ExcelImportFixedTemplate<TStagingRow>
{
    public required string ImportType { get; init; }
    public required ExcelImportWorkbookDefinition WorkbookDefinition { get; init; }
    public required Func<IReadOnlyDictionary<string, string>, IEnumerable<string>> ValidateRow { get; init; }
    public required Func<IReadOnlyDictionary<string, string>, string> BuildRowSnapshot { get; init; }
    public required Func<ExcelImportStagingRowContext, TStagingRow> CreateStagingRow { get; init; }
    public string NoDataError { get; init; } = "Filen innehåller inga datarader att importera.";
}

public sealed class ExcelImportStagingRowContext
{
    public Guid ImportBatchId { get; init; }
    public int RowNo { get; init; }
    public required IReadOnlyDictionary<string, string> RowData { get; init; }
    public required IReadOnlyDictionary<string, string> NonEmptyRowData { get; init; }
    public required string ImportedBy { get; init; }
    public required ExcelImportUserContext UserContext { get; init; }
}

public sealed class ExcelImportFixedTemplateEngine : IExcelImportFixedTemplateEngine
{
    private const int MaxValidationErrors = 200;
    private const int MaxSnapshotLength = 1_000;

    private readonly IExcelImportContextService _contextService;
    private readonly IExcelImportWorkbookReader _workbookReader;
    private readonly IExcelImportResultFactory _resultFactory;

    public ExcelImportFixedTemplateEngine(
        IExcelImportContextService contextService,
        IExcelImportWorkbookReader workbookReader,
        IExcelImportResultFactory resultFactory)
    {
        _contextService = contextService;
        _workbookReader = workbookReader;
        _resultFactory = resultFactory;
    }

    public async Task<ExcelImportResult> ImportAsync<TStagingRow>(
        IFormFile file,
        string importedBy,
        ExcelImportFixedTemplate<TStagingRow> template,
        Func<IReadOnlyCollection<TStagingRow>, CancellationToken, Task> stageRowsAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(importedBy);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(stageRowsAsync);

        var userContext = ExcelImportContextGuard.GetRequiredCurrent(_contextService);
        var batchId = Guid.NewGuid();
        var workbook = await _workbookReader.ReadAsync(file, template.WorkbookDefinition, cancellationToken);
        if (workbook.Errors.Count > 0)
            return CreateResult(template, batchId, workbook.RowHeaders, errors: workbook.Errors);

        var stagingRows = new List<TStagingRow>(workbook.Rows.Count);
        var rowResults = new List<ExcelImportRowResult>(workbook.Rows.Count);
        var errors = new List<string>();
        var invalidRows = 0;

        foreach (var workbookRow in workbook.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowErrors = template.ValidateRow(workbookRow.Data).ToList();
            var isValid = rowErrors.Count == 0;
            rowResults.Add(new ExcelImportRowResult
            {
                RowNo = workbookRow.RowNo,
                IsValid = isValid,
                ErrorMessage = isValid ? null : string.Join(" ", rowErrors),
                Data = workbookRow.Data
            });

            if (!isValid)
            {
                invalidRows++;
                AddValidationErrors(
                    errors,
                    rowErrors,
                    workbookRow.RowNo,
                    template.BuildRowSnapshot(workbookRow.Data));
                continue;
            }

            stagingRows.Add(template.CreateStagingRow(new ExcelImportStagingRowContext
            {
                ImportBatchId = batchId,
                RowNo = workbookRow.RowNo,
                RowData = workbookRow.Data,
                NonEmptyRowData = BuildNonEmptyRowData(workbookRow.Data),
                ImportedBy = importedBy,
                UserContext = userContext
            }));
        }

        if (workbook.Rows.Count == 0)
            errors.Add(template.NoDataError);

        var validRows = workbook.Rows.Count - invalidRows;
        if (errors.Count > 0)
        {
            return CreateResult(
                template,
                batchId,
                workbook.RowHeaders,
                workbook.Rows.Count,
                validRows,
                invalidRows,
                rowResults,
                errors);
        }

        if (stagingRows.Count > 0)
            await stageRowsAsync(stagingRows, cancellationToken);

        return CreateResult(
            template,
            batchId,
            workbook.RowHeaders,
            workbook.Rows.Count,
            validRows,
            invalidRows,
            rowResults,
            stagedRows: stagingRows.Count);
    }

    private ExcelImportResult CreateResult<TStagingRow>(
        ExcelImportFixedTemplate<TStagingRow> template,
        Guid batchId,
        IReadOnlyList<string> headers,
        int totalRows = 0,
        int validRows = 0,
        int invalidRows = 0,
        IReadOnlyList<ExcelImportRowResult>? rowResults = null,
        IReadOnlyList<string>? errors = null,
        int stagedRows = 0)
        => _resultFactory.Create(new ExcelImportResultCreateRequest
        {
            ImportType = template.ImportType,
            ImportBatchId = batchId,
            TotalRows = totalRows,
            ValidRows = validRows,
            InvalidRows = invalidRows,
            StagedRows = stagedRows,
            RowHeaders = headers,
            RowResults = rowResults ?? Array.Empty<ExcelImportRowResult>(),
            Errors = errors ?? Array.Empty<string>()
        });

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

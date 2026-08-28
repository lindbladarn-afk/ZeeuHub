namespace WebApp.Services.ExcelImport;

// Builds shared Excel Import results with consistent metadata for UI and background jobs.
public interface IExcelImportResultFactory
{
    ExcelImportResult Create(ExcelImportResultCreateRequest request);
}

public sealed class ExcelImportResultCreateRequest
{
    public required string ImportType { get; init; }
    public Guid ImportBatchId { get; init; }
    public Guid? EditSessionId { get; init; }
    public int TotalRows { get; init; }
    public int ValidRows { get; init; }
    public int InvalidRows { get; init; }
    public int StagedRows { get; init; }
    public IReadOnlyList<string> RowHeaders { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ExcelImportRowResult> RowResults { get; init; } = Array.Empty<ExcelImportRowResult>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

// Centralizes shared Excel Import result shaping for import and edit-session services.
public sealed class ExcelImportResultFactory : IExcelImportResultFactory
{
    public ExcelImportResult Create(ExcelImportResultCreateRequest request)
    {
        return new ExcelImportResult
        {
            ImportType = request.ImportType,
            ImportBatchId = request.ImportBatchId,
            EditSessionId = request.EditSessionId,
            TotalRows = request.TotalRows,
            ValidRows = request.ValidRows,
            InvalidRows = request.InvalidRows,
            StagedRows = request.StagedRows,
            RowHeaders = request.RowHeaders.ToList(),
            RowResults = request.RowResults.ToList(),
            Errors = request.Errors.ToList()
        };
    }
}

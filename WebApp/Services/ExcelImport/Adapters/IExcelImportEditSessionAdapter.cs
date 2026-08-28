using Microsoft.AspNetCore.Http;

namespace WebApp.Services.ExcelImport;

// Defines the edit-session workflow each Excel import type exposes to controllers and background jobs.
public interface IExcelImportEditSessionAdapter
{
    string ImportType { get; }
    string EditSessionFileName { get; }
    int MaxEditableRows { get; }

    Task<ExcelImportResult> CreateEditSessionFromFileAsync(
        IFormFile file,
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken = default);

    Task<ExcelImportResult> CreateEmptyEditSessionAsync(
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken = default);

    Task<ExcelImportResult> ImportEditedRowsAsync(
        Guid editSessionId,
        string rowsJson,
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken = default);

    Task<ExcelImportResult> TryAutoImportEditedRowsAsync(
        ExcelImportResult result,
        string importedBy,
        ExcelImportEditSessionContext context,
        CancellationToken cancellationToken = default);
}

public sealed class ExcelImportEditSessionContext
{
    public DateTime? VoucherPostingDate { get; init; }
    public DateTime? VoucherReversalDate { get; init; }
}

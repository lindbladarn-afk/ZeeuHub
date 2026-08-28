using Microsoft.AspNetCore.Http;
using WebApp.Models.BackgroundJobs;

namespace WebApp.Services.ExcelImport;

// Orchestrates Excel Import upload, execution, and edit-session flows behind one application entry point.
public interface IExcelImportService
{
    bool IsSupportedImportType(string? importType);
    bool IsEditSessionSupported(string? importType);
    ExcelImportEditSessionInfo GetEditSessionInfo(string importType);

    Task<BackgroundJobSnapshot> QueueUploadAsync(
        IFormFile file,
        ExcelImportUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<ExcelImportResult> RunAsync(
        IFormFile file,
        ExcelImportRunRequest request,
        CancellationToken cancellationToken = default);

    Task<ExcelImportResult> ImportDirectAsync(
        string importType,
        IFormFile file,
        string importedBy,
        CancellationToken cancellationToken = default);

    Task<ExcelImportResult> CreateEmptyEditSessionAsync(
        ExcelImportEditSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<ExcelImportResult> ImportEditedRowsAsync(
        ExcelImportEditedRowsRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ExcelImportUploadRequest
{
    public required string ImportType { get; init; }
    public required string ImportedBy { get; init; }
    public string? CreatedByUserId { get; init; }
    public string? CreatedByEmail { get; init; }
    public Guid CompanyId { get; init; }
    public int? JeevesActiveCompany { get; init; }
    public string? VoucherPostingDate { get; init; }
    public string? VoucherReversalDate { get; init; }
}

public sealed class ExcelImportRunRequest
{
    public required string ImportType { get; init; }
    public required string ImportedBy { get; init; }
    public string? VoucherPostingDate { get; init; }
    public string? VoucherReversalDate { get; init; }
}

public sealed class ExcelImportEditSessionRequest
{
    public required string ImportType { get; init; }
    public required string ImportedBy { get; init; }
    public ExcelImportEditSessionContext Context { get; init; } = new();
}

public sealed class ExcelImportEditedRowsRequest
{
    public required string ImportType { get; init; }
    public required Guid EditSessionId { get; init; }
    public required string RowsJson { get; init; }
    public required string ImportedBy { get; init; }
    public ExcelImportEditSessionContext Context { get; init; } = new();
}

public sealed class ExcelImportEditSessionInfo
{
    public required string ImportType { get; init; }
    public required string EditSessionFileName { get; init; }
    public int MaxEditableRows { get; init; }
}

public sealed class ExcelImportService : IExcelImportService
{
    private readonly IReadOnlyDictionary<string, IExcelImportHandler> _handlers;
    private readonly ExcelImportEditSessionAdapterResolver _editSessionAdapters;
    private readonly IExcelImportBackgroundJobScheduler _backgroundJobScheduler;

    public ExcelImportService(
        IEnumerable<IExcelImportHandler> handlers,
        ExcelImportEditSessionAdapterResolver editSessionAdapters,
        IExcelImportBackgroundJobScheduler backgroundJobScheduler)
    {
        _handlers = handlers.ToDictionary(handler => handler.ImportType, StringComparer.OrdinalIgnoreCase);
        _editSessionAdapters = editSessionAdapters;
        _backgroundJobScheduler = backgroundJobScheduler;
    }

    public bool IsSupportedImportType(string? importType)
    {
        var type = NormalizeImportType(importType);
        return _handlers.ContainsKey(type) || _editSessionAdapters.Find(type) is not null;
    }

    public bool IsEditSessionSupported(string? importType)
        => _editSessionAdapters.Find(NormalizeImportType(importType)) is not null;

    public ExcelImportEditSessionInfo GetEditSessionInfo(string importType)
    {
        var adapter = _editSessionAdapters.GetRequired(NormalizeImportType(importType));
        return new ExcelImportEditSessionInfo
        {
            ImportType = adapter.ImportType,
            EditSessionFileName = adapter.EditSessionFileName,
            MaxEditableRows = adapter.MaxEditableRows
        };
    }

    public Task<BackgroundJobSnapshot> QueueUploadAsync(
        IFormFile file,
        ExcelImportUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(request);

        return _backgroundJobScheduler.EnqueueAsync(
            file,
            new ExcelImportBackgroundJobPayload
            {
                ImportType = NormalizeImportType(request.ImportType),
                ImportedBy = request.ImportedBy,
                CreatedByUserId = request.CreatedByUserId,
                CreatedByEmail = request.CreatedByEmail,
                CompanyId = request.CompanyId,
                JeevesActiveCompany = request.JeevesActiveCompany,
                VoucherPostingDate = request.VoucherPostingDate,
                VoucherReversalDate = request.VoucherReversalDate
            },
            cancellationToken);
    }

    public async Task<ExcelImportResult> RunAsync(
        IFormFile file,
        ExcelImportRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(request);

        var type = NormalizeImportType(request.ImportType);
        var context = BuildContext(type, request.VoucherPostingDate, request.VoucherReversalDate);
        var adapter = _editSessionAdapters.Find(type);
        if (adapter is not null)
        {
            var result = await adapter.CreateEditSessionFromFileAsync(file, request.ImportedBy, context, cancellationToken);
            return await adapter.TryAutoImportEditedRowsAsync(result, request.ImportedBy, context, cancellationToken);
        }

        return await ImportDirectAsync(type, file, request.ImportedBy, cancellationToken);
    }

    public Task<ExcelImportResult> CreateEmptyEditSessionAsync(
        ExcelImportEditSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var adapter = _editSessionAdapters.GetRequired(NormalizeImportType(request.ImportType));
        return adapter.CreateEmptyEditSessionAsync(request.ImportedBy, request.Context, cancellationToken);
    }

    public Task<ExcelImportResult> ImportEditedRowsAsync(
        ExcelImportEditedRowsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var adapter = _editSessionAdapters.GetRequired(NormalizeImportType(request.ImportType));
        return adapter.ImportEditedRowsAsync(
            request.EditSessionId,
            request.RowsJson,
            request.ImportedBy,
            request.Context,
            cancellationToken);
    }

    public Task<ExcelImportResult> ImportDirectAsync(
        string importType,
        IFormFile file,
        string importedBy,
        CancellationToken cancellationToken = default)
    {
        var type = NormalizeImportType(importType);
        if (!_handlers.TryGetValue(type, out var handler))
        {
            throw new InvalidOperationException($"Ingen handler registrerad för importtyp '{importType}'.");
        }

        return handler.ImportAsync(file, importedBy, cancellationToken);
    }

    private static ExcelImportEditSessionContext BuildContext(
        string importType,
        string? voucherPostingDate,
        string? voucherReversalDate)
    {
        if (!string.Equals(importType, "voucher", StringComparison.OrdinalIgnoreCase))
            return new ExcelImportEditSessionContext();

        if (!ExcelImportDateParser.TryParsePostingDate(voucherPostingDate, out var parsedPostingDate))
            throw new InvalidOperationException("Bokföringsdatum saknas eller är ogiltigt.");

        if (!ExcelImportDateParser.TryParseOptionalDate(voucherReversalDate, out var parsedReversalDate))
            throw new InvalidOperationException("Återbokningsdatum måste vara ett giltigt datum.");

        return new ExcelImportEditSessionContext
        {
            VoucherPostingDate = parsedPostingDate.Date,
            VoucherReversalDate = parsedReversalDate?.Date
        };
    }

    private static string NormalizeImportType(string? importType)
        => (importType ?? "voucher").Trim().ToLowerInvariant();
}

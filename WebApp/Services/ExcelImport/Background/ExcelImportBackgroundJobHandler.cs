// Executes queued imports with tenant-scoped, data-minimized operational telemetry.
using System.Diagnostics;
using Entities.Application;
using Microsoft.AspNetCore.Http.Features;
using WebApp.Models.BackgroundJobs;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Observability;
using WebApp.Services.Integration;

namespace WebApp.Services.ExcelImport;

// Runs queued Excel imports and publishes progress to the sidebar runtime status menu.
public sealed class ExcelImportBackgroundJobHandler : IBackgroundJobHandler
{
    private const int MaxResultRows = 50;

    private readonly IExcelImportService _excelImportService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IExcelImportBackgroundFileStore _fileStore;
    private readonly IExcelImportRowResultStore _rowResultStore;
    private readonly ILogger<ExcelImportBackgroundJobHandler> _logger;

    public ExcelImportBackgroundJobHandler(
        IExcelImportService excelImportService,
        IHttpContextAccessor httpContextAccessor,
        IExcelImportBackgroundFileStore fileStore,
        IExcelImportRowResultStore rowResultStore,
        ILogger<ExcelImportBackgroundJobHandler> logger)
    {
        _excelImportService = excelImportService;
        _httpContextAccessor = httpContextAccessor;
        _fileStore = fileStore;
        _rowResultStore = rowResultStore;
        _logger = logger;
    }

    public string JobType => ExcelImportBackgroundJobConstants.ExecuteJobType;

    public async Task<BackgroundJobHandlerResult> HandleAsync(BackgroundJobSnapshot job, CancellationToken cancellationToken)
    {
        var payload = ExcelImportBackgroundJobPayload.FromJson(job.PayloadJson);
        var supportId = ResolveSupportId(job);
        var timer = Stopwatch.StartNew();
        var deleteStoredFile = true;
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["JobId"] = job.Id,
            ["CompanyId"] = payload.CompanyId,
            ["SupportId"] = supportId,
            ["JeevesCompanyCode"] = payload.JeevesActiveCompany,
            ["CorrelationId"] = job.CorrelationKey,
            ["Module"] = "ExcelImport",
            ["Operation"] = payload.ImportType
        });

        _logger.LogInformation(
            "Excel import started. {JobId} {CompanyId} {JeevesCompanyCode} {ImportType} {FileSizeBytes}",
            job.Id,
            payload.CompanyId,
            payload.JeevesActiveCompany,
            payload.ImportType,
            payload.FileSizeBytes);

        try
        {
            using var contextScope = ApplyBackgroundHttpContext(payload);
            var result = await ExecuteImportAsync(payload, cancellationToken);
            await PersistStandardRowResultsAsync(payload, result, cancellationToken);
            var persistedResultJson = BuildResultJson(result, payload.ImportType, includeRows: false);
            var runtimeResultJson = BuildResultJson(result, payload.ImportType, includeRows: true);
            if ((result.Errors?.Any() ?? false) || (result.ValidRows == 0 && result.InvalidRows > 0))
            {
                var summary = BuildFailureSummary(payload, result);
                _logger.LogWarning(
                    "Excel import validation failed. {ErrorCode} {JobId} {CompanyId} {ImportType} {DurationMs} {TotalRows} {ValidRows} {InvalidRows} {Result}",
                    PortalErrorCodes.ExcelImportValidationFailed,
                    job.Id,
                    payload.CompanyId,
                    payload.ImportType,
                    timer.ElapsedMilliseconds,
                    result.TotalRows,
                    result.ValidRows,
                    result.InvalidRows,
                    "ValidationFailed");
                return BackgroundJobHandlerResult.Failure(
                    PortalErrorCodes.ExcelImportValidationFailed,
                    $"{summary} Referens: {supportId}.",
                    persistedResultJson,
                    runtimeResultJson);
            }

            _logger.LogInformation(
                "Excel import completed. {JobId} {CompanyId} {ImportType} {DurationMs} {TotalRows} {ValidRows} {InvalidRows} {Result}",
                job.Id,
                payload.CompanyId,
                payload.ImportType,
                timer.ElapsedMilliseconds,
                result.TotalRows,
                result.ValidRows,
                result.InvalidRows,
                "Succeeded");
            return BackgroundJobHandlerResult.Success(persistedResultJson, runtimeResultJson);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            deleteStoredFile = false;
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Excel import failed. {ErrorCode} {JobId} {CompanyId} {ImportType} {DurationMs}",
                PortalErrorCodes.ExcelImportProcessingFailed,
                job.Id,
                payload.CompanyId,
                payload.ImportType,
                timer.ElapsedMilliseconds);
            var diagnostic = IntegrationLogSanitizer.Diagnostic(ex.Message);
            var summary = string.IsNullOrWhiteSpace(payload.OriginalFileName)
                ? diagnostic
                : $"Import av {Path.GetFileName(payload.OriginalFileName)} misslyckades: {diagnostic}";
            var canRetry = IsTransientProcessingFailure(ex) && job.AttemptCount < job.MaxAttempts;
            if (canRetry)
            {
                deleteStoredFile = false;
                return BackgroundJobHandlerResult.Retry(
                    PortalErrorCodes.ExcelImportProcessingFailed,
                    $"{summary} Referens: {supportId}.",
                    TimeSpan.FromSeconds(30));
            }

            return BackgroundJobHandlerResult.Failure(
                PortalErrorCodes.ExcelImportProcessingFailed,
                $"{summary} Referens: {supportId}.");
        }
        finally
        {
            if (deleteStoredFile)
                _fileStore.DeleteQuietly(payload.FilePath);
        }
    }

    private async Task<ExcelImportResult> ExecuteImportAsync(ExcelImportBackgroundJobPayload payload, CancellationToken cancellationToken)
    {
        await using var file = OpenFormFile(payload);
        return await _excelImportService.RunAsync(
            file,
            new ExcelImportRunRequest
            {
                ImportType = payload.ImportType,
                ImportedBy = payload.ImportedBy,
                VoucherPostingDate = payload.VoucherPostingDate,
                VoucherReversalDate = payload.VoucherReversalDate
            },
            cancellationToken);
    }

    private ExcelImportFormFile OpenFormFile(ExcelImportBackgroundJobPayload payload)
        => new(payload.FilePath, payload.OriginalFileName);

    private Task PersistStandardRowResultsAsync(
        ExcelImportBackgroundJobPayload payload,
        ExcelImportResult result,
        CancellationToken cancellationToken)
    {
        var importType = ExcelImportTypeDefinitions.Normalize(payload.ImportType);
        if (importType is "transautoprice" or "presskogyoprice" || result.RowResults.Count == 0)
            return Task.CompletedTask;

        var importedAt = DateTime.UtcNow;
        var rows = result.RowResults.Select(row => new ExcelImportStoredRowResult
        {
            ImportType = importType,
            ImportBatchId = result.ImportBatchId,
            RowNo = row.RowNo,
            IsValid = row.IsValid,
            Data = row.Data,
            ErrorMessage = row.ErrorMessage,
            ImportedAt = importedAt,
            CompanyId = payload.CompanyId,
            UserId = payload.CreatedByUserId ?? payload.ImportedBy
        });
        return _rowResultStore.BulkInsertAsync(rows, cancellationToken);
    }

    private IDisposable ApplyBackgroundHttpContext(ExcelImportBackgroundJobPayload payload)
    {
        var previous = _httpContextAccessor.HttpContext;
        var context = new DefaultHttpContext();
        context.Features.Set<ISessionFeature>(new BackgroundSessionFeature
        {
            Session = new ExcelImportBackgroundSession(new UserSession
            {
                UserId = payload.CreatedByUserId ?? payload.ImportedBy,
                Email = payload.CreatedByEmail,
                CompanyId = payload.CompanyId,
                JeevesActiveCompany = payload.JeevesActiveCompany
            })
        });
        _httpContextAccessor.HttpContext = context;
        return new RestoreHttpContextScope(_httpContextAccessor, previous);
    }

    private static string BuildFailureSummary(ExcelImportBackgroundJobPayload payload, ExcelImportResult result)
    {
        var fileName = Path.GetFileName(payload.OriginalFileName);
        var firstError = result.Errors?.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstError))
            return $"Import av {fileName} stoppades: {IntegrationLogSanitizer.Diagnostic(firstError)}";

        return $"Import av {fileName} stoppades. Ogiltiga rader: {result.InvalidRows}.";
    }

    private static string BuildResultJson(ExcelImportResult result, string? importType, bool includeRows)
    {
        var rows = includeRows
            ? result.RowResults.Take(MaxResultRows).Select(row => (object)new
            {
                row.RowNo,
                row.IsValid,
                row.ErrorMessage,
                row.Data
            }).ToList()
            : new List<object>();

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            ImportType = importType ?? result.ImportType,
            result.ImportBatchId,
            result.TotalRows,
            result.ValidRows,
            result.InvalidRows,
            result.StagedRows,
            ErrorCount = result.Errors?.Count ?? 0,
            FirstError = IntegrationLogSanitizer.Diagnostic(result.Errors?.FirstOrDefault()),
            result.RowHeaders,
            Rows = rows
        });
    }

    private static string ResolveSupportId(BackgroundJobSnapshot job)
    {
        var supportId = job.Id.ToString("N");
        return supportId.Length >= 8 ? supportId[..8] : supportId;
    }

    private static bool IsTransientProcessingFailure(Exception exception)
        => exception is IOException or TimeoutException or Microsoft.Data.SqlClient.SqlException;

    private sealed class RestoreHttpContextScope : IDisposable
    {
        private readonly IHttpContextAccessor _accessor;
        private readonly HttpContext? _previous;

        public RestoreHttpContextScope(IHttpContextAccessor accessor, HttpContext? previous)
        {
            _accessor = accessor;
            _previous = previous;
        }

        public void Dispose()
        {
            _accessor.HttpContext = _previous;
        }
    }

    private sealed class BackgroundSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new ExcelImportBackgroundSession(new UserSession());
    }

    private sealed class ExcelImportFormFile : IFormFile, IAsyncDisposable
    {
        private readonly string _path;
        private readonly long _length;

        public ExcelImportFormFile(string path, string fileName)
        {
            _path = path;
            _length = new FileInfo(path).Length;
            FileName = fileName;
            Name = "file";
            Headers = new HeaderDictionary();
            ContentType = "application/octet-stream";
        }

        public string ContentType { get; }
        public string ContentDisposition { get; } = string.Empty;
        public IHeaderDictionary Headers { get; }
        public long Length => _length;
        public string Name { get; }
        public string FileName { get; }
        public Stream OpenReadStream() => OpenFile();
        public void CopyTo(Stream target)
        {
            using var input = OpenFile();
            input.CopyTo(target);
        }

        public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            await using var input = OpenFile();
            await input.CopyToAsync(target, cancellationToken);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private FileStream OpenFile()
            => new(
                _path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
    }
}

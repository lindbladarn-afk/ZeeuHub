using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebApp.Models.BackgroundJobs;
using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

// Verifies safe failures and cancellation behavior for queued Excel imports.
public sealed class ExcelImportBackgroundJobObservabilityTests
{
    [Fact]
    public async Task HandleAsync_SanitizesFailureAndLogsStructuredError()
    {
        var path = Path.GetTempFileName();
        var logger = new CapturingLogger<ExcelImportBackgroundJobHandler>();
        var handler = new ExcelImportBackgroundJobHandler(
            new ThrowingExcelImportService(),
            new HttpContextAccessor(),
            new DeletingFileStore(),
            new CapturingRowResultStore(),
            logger);
        var companyId = Guid.NewGuid();
        var jobId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var job = new BackgroundJobSnapshot
        {
            Id = jobId,
            CompanyId = companyId,
            CorrelationKey = "excel-test",
            PayloadJson = new ExcelImportBackgroundJobPayload
            {
                FilePath = path,
                OriginalFileName = "import.xlsx",
                FileSizeBytes = 10,
                ImportType = "prices",
                ImportedBy = "user-id",
                CompanyId = companyId,
                JeevesActiveCompany = 10
            }.ToJson()
        };

        var result = await handler.HandleAsync(job, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("EXCEL_IMPORT_PROCESSING_FAILED", result.ErrorCode);
        Assert.Contains("Referens: 11111111", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("[redacted]", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", result.ErrorMessage, StringComparison.Ordinal);
        var error = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.IsType<InvalidOperationException>(error.Exception);
        Assert.Equal("EXCEL_IMPORT_PROCESSING_FAILED", error.Properties["ErrorCode"]?.ToString());
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task HandleAsync_PreservesStoredFile_WhenWorkerIsCanceled()
    {
        var path = Path.GetTempFileName();
        try
        {
            var handler = new ExcelImportBackgroundJobHandler(
                new CancelingExcelImportService(),
                new HttpContextAccessor(),
                new DeletingFileStore(),
                new CapturingRowResultStore(),
                new CapturingLogger<ExcelImportBackgroundJobHandler>());
            var companyId = Guid.NewGuid();
            var job = new BackgroundJobSnapshot
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                PayloadJson = new ExcelImportBackgroundJobPayload
                {
                    FilePath = path,
                    OriginalFileName = "import.xlsx",
                    FileSizeBytes = 10,
                    ImportType = "budget",
                    ImportedBy = "user-id",
                    CompanyId = companyId,
                    JeevesActiveCompany = 10
                }.ToJson()
            };
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                handler.HandleAsync(job, cancellation.Token));

            Assert.True(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task HandleAsync_KeepsRowPreviewOutOfPersistedResult()
    {
        var path = Path.GetTempFileName();
        var companyId = Guid.NewGuid();
        var rowResultStore = new CapturingRowResultStore();
        var handler = new ExcelImportBackgroundJobHandler(
            new SuccessfulExcelImportService(),
            new HttpContextAccessor(),
            new DeletingFileStore(),
            rowResultStore,
            new CapturingLogger<ExcelImportBackgroundJobHandler>());
        var job = new BackgroundJobSnapshot
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PayloadJson = new ExcelImportBackgroundJobPayload
            {
                FilePath = path,
                OriginalFileName = "import.xlsx",
                FileSizeBytes = 10,
                ImportType = "budget",
                ImportedBy = "user-id",
                CompanyId = companyId,
                JeevesActiveCompany = 10
            }.ToJson()
        };

        var result = await handler.HandleAsync(job, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain("sensitive-row-value", result.ResultJson, StringComparison.Ordinal);
        Assert.Contains("sensitive-row-value", result.RuntimeResultJson, StringComparison.Ordinal);
        var storedRow = Assert.Single(rowResultStore.Rows);
        Assert.Equal(companyId, storedRow.CompanyId);
        Assert.Equal("sensitive-row-value", storedRow.Data["Account"]);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task HandleAsync_PreservesFileAndRequestsRetryForTransientFailure()
    {
        var path = Path.GetTempFileName();
        try
        {
            var companyId = Guid.NewGuid();
            var handler = new ExcelImportBackgroundJobHandler(
                new TransientlyFailingExcelImportService(),
                new HttpContextAccessor(),
                new DeletingFileStore(),
                new CapturingRowResultStore(),
                new CapturingLogger<ExcelImportBackgroundJobHandler>());
            var job = new BackgroundJobSnapshot
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                AttemptCount = 1,
                MaxAttempts = 3,
                PayloadJson = new ExcelImportBackgroundJobPayload
                {
                    FilePath = path,
                    OriginalFileName = "import.xlsx",
                    FileSizeBytes = 10,
                    ImportType = "budget",
                    ImportedBy = "user-id",
                    CompanyId = companyId,
                    JeevesActiveCompany = 10
                }.ToJson()
            };

            var result = await handler.HandleAsync(job, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal(TimeSpan.FromSeconds(30), result.RetryDelay);
            Assert.True(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class ThrowingExcelImportService : IExcelImportService
    {
        public bool IsSupportedImportType(string? importType) => true;
        public bool IsEditSessionSupported(string? importType) => false;
        public ExcelImportEditSessionInfo GetEditSessionInfo(string importType) => throw new NotSupportedException();
        public Task<BackgroundJobSnapshot> QueueUploadAsync(IFormFile file, ExcelImportUploadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> RunAsync(IFormFile file, ExcelImportRunRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("authorization=secret-value");
        public Task<ExcelImportResult> ImportDirectAsync(string importType, IFormFile file, string importedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> CreateEmptyEditSessionAsync(ExcelImportEditSessionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportEditedRowsAsync(ExcelImportEditedRowsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CancelingExcelImportService : IExcelImportService
    {
        public bool IsSupportedImportType(string? importType) => true;
        public bool IsEditSessionSupported(string? importType) => false;
        public ExcelImportEditSessionInfo GetEditSessionInfo(string importType) => throw new NotSupportedException();
        public Task<BackgroundJobSnapshot> QueueUploadAsync(IFormFile file, ExcelImportUploadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> RunAsync(IFormFile file, ExcelImportRunRequest request, CancellationToken cancellationToken = default) => Task.FromCanceled<ExcelImportResult>(cancellationToken);
        public Task<ExcelImportResult> ImportDirectAsync(string importType, IFormFile file, string importedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> CreateEmptyEditSessionAsync(ExcelImportEditSessionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportEditedRowsAsync(ExcelImportEditedRowsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class SuccessfulExcelImportService : IExcelImportService
    {
        public bool IsSupportedImportType(string? importType) => true;
        public bool IsEditSessionSupported(string? importType) => false;
        public ExcelImportEditSessionInfo GetEditSessionInfo(string importType) => throw new NotSupportedException();
        public Task<BackgroundJobSnapshot> QueueUploadAsync(IFormFile file, ExcelImportUploadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> RunAsync(IFormFile file, ExcelImportRunRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ExcelImportResult
            {
                ImportType = request.ImportType,
                ImportBatchId = Guid.NewGuid(),
                TotalRows = 1,
                ValidRows = 1,
                RowHeaders = ["Account"],
                RowResults =
                [
                    new ExcelImportRowResult
                    {
                        RowNo = 2,
                        IsValid = true,
                        Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Account"] = "sensitive-row-value"
                        }
                    }
                ]
            });
        public Task<ExcelImportResult> ImportDirectAsync(string importType, IFormFile file, string importedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> CreateEmptyEditSessionAsync(ExcelImportEditSessionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportEditedRowsAsync(ExcelImportEditedRowsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TransientlyFailingExcelImportService : IExcelImportService
    {
        public bool IsSupportedImportType(string? importType) => true;
        public bool IsEditSessionSupported(string? importType) => false;
        public ExcelImportEditSessionInfo GetEditSessionInfo(string importType) => throw new NotSupportedException();
        public Task<BackgroundJobSnapshot> QueueUploadAsync(IFormFile file, ExcelImportUploadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> RunAsync(IFormFile file, ExcelImportRunRequest request, CancellationToken cancellationToken = default) => throw new IOException("Temporary storage failure.");
        public Task<ExcelImportResult> ImportDirectAsync(string importType, IFormFile file, string importedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> CreateEmptyEditSessionAsync(ExcelImportEditSessionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExcelImportResult> ImportEditedRowsAsync(ExcelImportEditedRowsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class DeletingFileStore : IExcelImportBackgroundFileStore
    {
        public Task<StoredExcelImportFile> SaveAsync(IFormFile file, Guid companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void DeleteQuietly(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }

        public int CleanupExpired(
            DateTime cutoffUtc,
            IReadOnlySet<string> protectedPaths,
            CancellationToken cancellationToken) => 0;
    }

    private sealed class CapturingRowResultStore : IExcelImportRowResultStore
    {
        public List<ExcelImportStoredRowResult> Rows { get; } = [];

        public Task<bool> TableExistsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task BulkInsertAsync(
            IEnumerable<ExcelImportStoredRowResult> rows,
            CancellationToken cancellationToken = default)
        {
            Rows.AddRange(rows);
            return Task.CompletedTask;
        }

        public Task CleanupOldRowsAsync(int retentionDays = 7, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ExcelImportStoredRowPage> GetPageAsync(
            Guid companyId,
            string importType,
            Guid importBatchId,
            int page,
            int pageSize,
            bool showOnlyInvalidRows,
            bool showAllRows = false,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ExcelImportStoredRowPage());
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
            Entries.Add(new LogEntry(logLevel, exception, properties));
        }
    }

    private sealed record LogEntry(LogLevel Level, Exception? Exception, IReadOnlyDictionary<string, object?> Properties);
}

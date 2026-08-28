using System.Diagnostics;
using WebApp.Models.BackgroundJobs;
using WebApp.Services.Application.BackgroundJobs;

namespace WebApp.Services.ExcelImport;

// Queues uploaded Excel files for the shared background job worker.
public interface IExcelImportBackgroundJobScheduler
{
    Task<BackgroundJobSnapshot> EnqueueAsync(
        IFormFile file,
        ExcelImportBackgroundJobPayload payload,
        CancellationToken cancellationToken);
}

// Queues uploaded Excel files for the shared background job worker.
public sealed class ExcelImportBackgroundJobScheduler : IExcelImportBackgroundJobScheduler
{
    private readonly IExcelImportBackgroundFileStore _fileStore;
    private readonly IBackgroundJobStore _backgroundJobStore;
    private readonly IBackgroundJobRuntimeEventPublisher _runtimeEventPublisher;
    private readonly ILogger<ExcelImportBackgroundJobScheduler> _logger;

    public ExcelImportBackgroundJobScheduler(
        IExcelImportBackgroundFileStore fileStore,
        IBackgroundJobStore backgroundJobStore,
        IBackgroundJobRuntimeEventPublisher runtimeEventPublisher,
        ILogger<ExcelImportBackgroundJobScheduler> logger)
    {
        _fileStore = fileStore;
        _backgroundJobStore = backgroundJobStore;
        _runtimeEventPublisher = runtimeEventPublisher;
        _logger = logger;
    }

    public async Task<BackgroundJobSnapshot> EnqueueAsync(
        IFormFile file,
        ExcelImportBackgroundJobPayload payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(payload);

        var total = Stopwatch.StartNew();
        var save = Stopwatch.StartNew();
        var storedFile = await _fileStore.SaveAsync(file, payload.CompanyId, cancellationToken);
        _logger.LogInformation(
            "Excel import upload stored. {CompanyId} {ImportType} {FileSizeBytes} {DurationMs}",
            payload.CompanyId,
            payload.ImportType,
            storedFile.SizeBytes,
            save.ElapsedMilliseconds);

        payload.FilePath = storedFile.Path;
        payload.OriginalFileName = storedFile.OriginalFileName;
        payload.FileSizeBytes = storedFile.SizeBytes;

        try
        {
            var enqueue = Stopwatch.StartNew();
            var job = _backgroundJobStore.Enqueue(
                new BackgroundJobEnqueueRequest
                {
                    CompanyId = payload.CompanyId,
                    CreatedByUserId = payload.CreatedByUserId,
                    CreatedByEmail = payload.CreatedByEmail,
                    JobType = ExcelImportBackgroundJobConstants.ExecuteJobType,
                    CorrelationKey = $"excel-import:{payload.CompanyId:N}:{Guid.NewGuid():N}",
                    PayloadJson = payload.ToJson(),
                    MaxAttempts = 3
                },
                DateTime.UtcNow);
            _logger.LogInformation(
                "Excel import job enqueued. {JobId} {CompanyId} {ImportType} {DurationMs}",
                job.Id,
                payload.CompanyId,
                payload.ImportType,
                enqueue.ElapsedMilliseconds);

            _runtimeEventPublisher.Publish(job, BackgroundJobStatus.Queued);
            _logger.LogInformation(
                "Excel import queue flow completed. {JobId} {CompanyId} {ImportType} {DurationMs}",
                job.Id,
                payload.CompanyId,
                payload.ImportType,
                total.ElapsedMilliseconds);

            return job;
        }
        catch
        {
            _fileStore.DeleteQuietly(storedFile.Path);
            throw;
        }
    }
}

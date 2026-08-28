using WebApp.Services.Application;
using WebApp.Models.BackgroundJobs;
using WebApp.Services.ExcelImport;
using WebApp.Observability;

namespace WebApp.Services.Application.BackgroundJobs;

// Centralizes sidebar event publication for all background job types with a registered presentation provider.
public sealed class BackgroundJobRuntimeEventPublisher : IBackgroundJobRuntimeEventPublisher
{
    private readonly IEnumerable<IBackgroundJobPresentationProvider> _presentationProviders;
    private readonly IExcelImportTransientStatusStore _excelImportTransientStatusStore;
    private readonly ISidebarRuntimeStatusService _sidebarRuntimeStatusService;
    private readonly ILogger<BackgroundJobRuntimeEventPublisher> _logger;

    public BackgroundJobRuntimeEventPublisher(
        IEnumerable<IBackgroundJobPresentationProvider> presentationProviders,
        IExcelImportTransientStatusStore excelImportTransientStatusStore,
        ISidebarRuntimeStatusService sidebarRuntimeStatusService,
        ILogger<BackgroundJobRuntimeEventPublisher> logger)
    {
        _presentationProviders = presentationProviders;
        _excelImportTransientStatusStore = excelImportTransientStatusStore;
        _sidebarRuntimeStatusService = sidebarRuntimeStatusService;
        _logger = logger;
    }

    public void Publish(BackgroundJobSnapshot job, BackgroundJobStatus status, string? resultJson = null, string? errorMessage = null)
    {
        var provider = _presentationProviders.FirstOrDefault(item => string.Equals(item.JobType, job.JobType, StringComparison.Ordinal));
        if (provider is null)
            return;

        try
        {
            var record = provider.CreateEvent(job, status, resultJson, errorMessage);
            if (record is null)
                return;

            if (string.Equals(provider.JobType, ExcelImportBackgroundJobConstants.ExecuteJobType, StringComparison.Ordinal))
            {
                _excelImportTransientStatusStore.Record(record);
                return;
            }

            _sidebarRuntimeStatusService.RecordEvent(
                job.CompanyId,
                record);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to publish background job runtime event. {ErrorCode} {JobId} {JobType} {CompanyId}",
                PortalErrorCodes.RuntimeEventPublishFailed,
                job.Id,
                job.JobType,
                job.CompanyId);
        }
    }
}

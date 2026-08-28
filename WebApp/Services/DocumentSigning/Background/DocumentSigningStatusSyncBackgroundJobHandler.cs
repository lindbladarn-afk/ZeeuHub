// Synchronizes signing status with safe, tenant-scoped operational telemetry.
using System.Diagnostics;
using WebApp.Models.BackgroundJobs;
using WebApp.Observability;
using WebApp.Repositories.DocumentSigning;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Services.Integration;

namespace WebApp.Services.DocumentSigning;

public sealed class DocumentSigningStatusSyncBackgroundJobHandler : IBackgroundJobHandler
{
    private static readonly TimeSpan NextSyncDelay = TimeSpan.FromMinutes(1);

    private readonly IDocumentSigningRepository _repository;
    private readonly IDocumentSigningService _documentSigningService;
    private readonly DocumentSigningStatusSyncJobScheduler _scheduler;
    private readonly ILogger<DocumentSigningStatusSyncBackgroundJobHandler> _logger;

    public DocumentSigningStatusSyncBackgroundJobHandler(
        IDocumentSigningRepository repository,
        IDocumentSigningService documentSigningService,
        DocumentSigningStatusSyncJobScheduler scheduler,
        ILogger<DocumentSigningStatusSyncBackgroundJobHandler> logger)
    {
        _repository = repository;
        _documentSigningService = documentSigningService;
        _scheduler = scheduler;
        _logger = logger;
    }

    public string JobType => DocumentSigningBackgroundJobConstants.StatusSyncJobType;

    public async Task<BackgroundJobHandlerResult> HandleAsync(BackgroundJobSnapshot job, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();

        try
        {
            var payload = DocumentSigningStatusSyncBackgroundJobPayload.Deserialize(job.PayloadJson);
            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["JobId"] = job.Id,
                ["CompanyId"] = job.CompanyId,
                ["CorrelationId"] = job.CorrelationKey,
                ["SigningId"] = payload.SigningId,
                ["Module"] = "DocumentSigning",
                ["Operation"] = "StatusSync",
                ["ExternalSystem"] = "Oneflow"
            });

            _logger.LogInformation(
                "Document signing status sync started. {JobId} {CompanyId} {SigningId} {ExternalSystem}",
                job.Id,
                job.CompanyId,
                payload.SigningId,
                "Oneflow");
            var signing = await _repository.GetByIdAsync(job.CompanyId, payload.SigningId, cancellationToken);
            if (signing == null)
            {
                _logger.LogInformation(
                    "Document signing status sync completed without a matching signing. {JobId} {CompanyId} {SigningId} {DurationMs} {Result}",
                    job.Id,
                    job.CompanyId,
                    payload.SigningId,
                    timer.ElapsedMilliseconds,
                    "NotFound");
                return BackgroundJobHandlerResult.Success();
            }

            var previousPortalStatus = signing.PortalStatus;
            var previousProviderStatus = signing.ProviderStatus;
            var previousSignedAndSealed = signing.SignedAndSealed;

            var updated = await _documentSigningService.SyncAsync(job.CompanyId, payload.SigningId, cancellationToken);
            if (updated == null)
                return BackgroundJobHandlerResult.Success();

            var statusChanged =
                !string.Equals(previousPortalStatus, updated.PortalStatus, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(previousProviderStatus, updated.ProviderStatus, StringComparison.OrdinalIgnoreCase)
                || previousSignedAndSealed != updated.SignedAndSealed;

            if (ShouldScheduleNextSync(updated.PortalStatus))
            {
                var refreshedSigning = await _repository.GetByIdAsync(job.CompanyId, payload.SigningId, cancellationToken);
                if (refreshedSigning != null)
                    _scheduler.EnqueueIfMissing(refreshedSigning, NextSyncDelay, job.Id);
            }

            _logger.LogInformation(
                "Document signing status sync completed. {JobId} {CompanyId} {SigningId} {DurationMs} {StatusChanged} {Result}",
                job.Id,
                job.CompanyId,
                payload.SigningId,
                timer.ElapsedMilliseconds,
                statusChanged,
                "Succeeded");

            return BackgroundJobHandlerResult.Success(
                statusChanged
                    ? DocumentSigningStatusSyncBackgroundJobResult.FromListItem(updated, statusChanged).ToJson()
                    : null);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Document signing status sync failed. {ErrorCode} {JobId} {CompanyId} {DurationMs}",
                PortalErrorCodes.DocumentSigningSyncFailed,
                job.Id,
                job.CompanyId,
                timer.ElapsedMilliseconds);
            return BackgroundJobHandlerResult.Retry(
                PortalErrorCodes.DocumentSigningSyncFailed,
                IntegrationLogSanitizer.Diagnostic(ex.Message),
                TimeSpan.FromMinutes(1));
        }
    }

    private static bool ShouldScheduleNextSync(string? portalStatus)
    {
        return string.Equals(portalStatus, "sent", StringComparison.OrdinalIgnoreCase)
            || string.Equals(portalStatus, "waitinginternal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(portalStatus, "preparing", StringComparison.OrdinalIgnoreCase);
    }
}

// Executes customer synchronization jobs with safe, structured operational telemetry.
using System.Diagnostics;
using WebApp.Models.BackgroundJobs;
using WebApp.Observability;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Services.Integration.CustomerSync.Application;
using WebApp.Services.Integration.CustomerSync.Domain;

namespace WebApp.Services.Integration.CustomerSync.Background;

// Executes queued customer sync jobs and leaves retry decisions to the shared worker.
public sealed class CustomerSyncBackgroundJobHandler : IBackgroundJobHandler
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(2);

    private readonly CustomerSyncFromJeevesHandler _fromJeevesHandler;
    private readonly CustomerSyncFromHubSpotHandler _fromHubSpotHandler;
    private readonly ILogger<CustomerSyncBackgroundJobHandler> _logger;

    public CustomerSyncBackgroundJobHandler(
        CustomerSyncFromJeevesHandler fromJeevesHandler,
        CustomerSyncFromHubSpotHandler fromHubSpotHandler,
        ILogger<CustomerSyncBackgroundJobHandler> logger)
    {
        _fromJeevesHandler = fromJeevesHandler;
        _fromHubSpotHandler = fromHubSpotHandler;
        _logger = logger;
    }

    public string JobType => CustomerSyncBackgroundJobConstants.ExecuteJobType;

    public async Task<BackgroundJobHandlerResult> HandleAsync(BackgroundJobSnapshot job, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();

        try
        {
            var payload = CustomerSyncBackgroundJobPayload.FromJson(job.PayloadJson);
            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["JobId"] = job.Id,
                ["CompanyId"] = payload.CompanyId,
                ["JeevesCompanyCode"] = payload.JeevesCompanyCode,
                ["CorrelationId"] = payload.CorrelationKey ?? job.CorrelationKey,
                ["Module"] = "CustomerSync",
                ["Operation"] = payload.Direction.ToString(),
                ["ExternalSystem"] = payload.Direction == CustomerSyncDirection.JeevesToHubSpot ? "HubSpot" : "Jeeves"
            });

            _logger.LogInformation(
                "Customer sync started. {JobId} {CompanyId} {JeevesCompanyCode} {Direction}",
                job.Id,
                payload.CompanyId,
                payload.JeevesCompanyCode,
                payload.Direction);

            var result = payload.Direction == CustomerSyncDirection.JeevesToHubSpot
                ? await _fromJeevesHandler.ExecuteAsync(
                    payload.CompanyId,
                    payload.JeevesCompanyCode,
                    payload.Trigger,
                    payload.CorrelationKey,
                    cancellationToken)
                : await ExecuteHubSpotAsync(payload, cancellationToken);

            _logger.LogInformation(
                "Customer sync completed. {JobId} {CompanyId} {JeevesCompanyCode} {Direction} {DurationMs} {CreatedCount} {UpdatedCount} {SkippedCount} {FailedCount} {Result}",
                job.Id,
                payload.CompanyId,
                payload.JeevesCompanyCode,
                payload.Direction,
                timer.ElapsedMilliseconds,
                result.CreatedCount,
                result.UpdatedCount,
                result.SkippedCount,
                result.FailedCount,
                result.Succeeded ? "Succeeded" : "PartialFailure");

            return BackgroundJobHandlerResult.Success(result.ToJson());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Customer sync failed. {ErrorCode} {JobId} {CompanyId} {DurationMs}",
                PortalErrorCodes.CustomerSyncFailed,
                job.Id,
                job.CompanyId,
                timer.ElapsedMilliseconds);
            return BackgroundJobHandlerResult.Retry(
                PortalErrorCodes.CustomerSyncFailed,
                IntegrationLogSanitizer.Diagnostic(ex.Message),
                RetryDelay);
        }
    }

    private Task<CustomerSyncResult> ExecuteHubSpotAsync(
        CustomerSyncBackgroundJobPayload payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.HubSpotEventId))
            throw new InvalidOperationException("HubSpot event id is required for HubSpot-to-Jeeves sync.");

        return _fromHubSpotHandler.ExecuteAsync(
            payload.CompanyId,
            payload.JeevesCompanyCode,
            payload.HubSpotEventId,
            payload.HubSpotObjectId,
            payload.CorrelationKey,
            cancellationToken);
    }
}

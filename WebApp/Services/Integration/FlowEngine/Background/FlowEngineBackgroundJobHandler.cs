// Executes FlowEngine jobs with structured status and failure telemetry.
using System.Diagnostics;
using WebApp.Models.BackgroundJobs;
using WebApp.Models.Integration;
using WebApp.Observability;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Services.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineBackgroundJobHandler : IBackgroundJobHandler
{
    private readonly FlowEngineQueuedJobProcessor _processor;
    private readonly ILogger<FlowEngineBackgroundJobHandler> _logger;

    public FlowEngineBackgroundJobHandler(
        FlowEngineQueuedJobProcessor processor,
        ILogger<FlowEngineBackgroundJobHandler> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    public string JobType => FlowEngineBackgroundJobConstants.ExecuteJobType;

    public async Task<BackgroundJobHandlerResult> HandleAsync(BackgroundJobSnapshot job, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();

        try
        {
            var payload = FlowEngineQueuedJobProcessor.DeserializePayload(job.PayloadJson);
            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["JobId"] = job.Id,
                ["CompanyId"] = payload.CompanyId,
                ["JeevesCompanyCode"] = payload.JeevesActiveCompany,
                ["CorrelationId"] = job.CorrelationKey,
                ["Module"] = "FlowEngine",
                ["Operation"] = payload.Request.Operation.ToString()
            });

            _logger.LogInformation(
                "FlowEngine job started. {JobId} {CompanyId} {JeevesCompanyCode} {Operation}",
                job.Id,
                payload.CompanyId,
                payload.JeevesActiveCompany,
                payload.Request.Operation);
            var result = await _processor.ProcessAsync(payload, cancellationToken);

            if (result.Status == FlowEngineJobStatus.Failed)
            {
                _logger.LogWarning(
                    "FlowEngine job returned failure. {ErrorCode} {JobId} {CompanyId} {Operation} {DurationMs} {Result}",
                    PortalErrorCodes.FlowEngineExecutionFailed,
                    job.Id,
                    payload.CompanyId,
                    payload.Request.Operation,
                    timer.ElapsedMilliseconds,
                    "Failed");
                return BackgroundJobHandlerResult.Failure(
                    PortalErrorCodes.FlowEngineExecutionFailed,
                    IntegrationLogSanitizer.Diagnostic(result.ErrorMessage ?? "FlowEngine execution failed."));
            }

            _logger.LogInformation(
                "FlowEngine job completed. {JobId} {CompanyId} {Operation} {DurationMs} {Result}",
                job.Id,
                payload.CompanyId,
                payload.Request.Operation,
                timer.ElapsedMilliseconds,
                "Succeeded");
            return BackgroundJobHandlerResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "FlowEngine background job failed. {ErrorCode} {JobId} {CompanyId} {DurationMs}",
                PortalErrorCodes.FlowEngineExecutionFailed,
                job.Id,
                job.CompanyId,
                timer.ElapsedMilliseconds);
            return BackgroundJobHandlerResult.Retry(
                PortalErrorCodes.FlowEngineExecutionFailed,
                IntegrationLogSanitizer.Diagnostic(ex.Message),
                TimeSpan.FromSeconds(20));
        }
    }
}

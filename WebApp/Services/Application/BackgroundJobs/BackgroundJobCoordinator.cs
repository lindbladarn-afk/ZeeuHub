using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WebApp.Models.BackgroundJobs;
using WebApp.Observability;
using WebApp.Services.Integration;

namespace WebApp.Services.Application.BackgroundJobs;

// Coordinates background jobs by company so different tenants can progress in parallel.
public sealed class BackgroundJobCoordinator : IBackgroundJobCoordinator
{
    private static readonly TimeSpan DefaultErrorDelay = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BackgroundJobWorkerOptions> _options;
    private readonly ILogger<BackgroundJobCoordinator> _logger;
    private readonly ConcurrentDictionary<Guid, Task> _activeCompanyLanes = new();
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public BackgroundJobCoordinator(
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundJobWorkerOptions> options,
        ILogger<BackgroundJobCoordinator> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var hasWork = await TickAsync(stoppingToken);
                await Task.Delay(hasWork ? _options.Value.DispatcherBusyDelay : _options.Value.DispatcherIdleDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background job coordinator loop failed unexpectedly.");
                await Task.Delay(DefaultErrorDelay, stoppingToken);
            }
        }
    }

    internal async Task<bool> TickAsync(CancellationToken stoppingToken)
    {
        CleanupCompletedLanes();

        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBackgroundJobStore>();
        var handlers = scope.ServiceProvider.GetServices<IBackgroundJobHandler>().ToArray();
        if (handlers.Length == 0)
        {
            return false;
        }

        var allowedJobTypes = handlers
            .Select(handler => handler.JobType)
            .Where(jobType => !string.IsNullOrWhiteSpace(jobType))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (allowedJobTypes.Length == 0)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        store.RequeueExpiredLeases(now, _options.Value.ExpiredLeaseRetryDelay);

        var queuedCompanies = store.ListQueuedCompanyIds(
            now,
            _options.Value.MaxDiscoveredCompaniesPerTick,
            allowedJobTypes);

        foreach (var companyId in queuedCompanies)
        {
            if (_activeCompanyLanes.Count >= _options.Value.MaxConcurrentCompanyLanes)
            {
                break;
            }

            if (_activeCompanyLanes.ContainsKey(companyId))
            {
                continue;
            }

            var laneTask = RunCompanyLaneAsync(companyId, allowedJobTypes, stoppingToken);
            if (!_activeCompanyLanes.TryAdd(companyId, laneTask))
            {
                continue;
            }

            _ = laneTask.ContinueWith(
                _ => _activeCompanyLanes.TryRemove(companyId, out _),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        return queuedCompanies.Count > 0 || _activeCompanyLanes.Count > 0;
    }

    private async Task RunCompanyLaneAsync(
        Guid companyId,
        IReadOnlyCollection<string> allowedJobTypes,
        CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBackgroundJobStore>();
        var handlers = scope.ServiceProvider.GetServices<IBackgroundJobHandler>()
            .ToDictionary(handler => handler.JobType, StringComparer.Ordinal);
        var runtimeEventPublisher = scope.ServiceProvider.GetRequiredService<IBackgroundJobRuntimeEventPublisher>();

        var idleRounds = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                store.RequeueExpiredLeases(now, _options.Value.ExpiredLeaseRetryDelay);

                var claimed = store.TryClaimNext(
                    _workerId,
                    now,
                    _options.Value.LeaseDuration,
                    companyId,
                    allowedJobTypes);

                if (claimed is null)
                {
                    idleRounds++;
                    if (idleRounds >= _options.Value.CompanyLaneIdleAttempts)
                    {
                        return;
                    }

                    await Task.Delay(_options.Value.CompanyLaneIdleDelay, stoppingToken);
                    continue;
                }

                idleRounds = 0;
                await HandleClaimedJobAsync(store, runtimeEventPublisher, handlers, claimed, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Company lane failed. {CompanyId}",
                    companyId);
                await Task.Delay(DefaultErrorDelay, stoppingToken);
            }
        }
    }

    private async Task HandleClaimedJobAsync(
        IBackgroundJobStore store,
        IBackgroundJobRuntimeEventPublisher runtimeEventPublisher,
        IReadOnlyDictionary<string, IBackgroundJobHandler> handlers,
        BackgroundJobSnapshot claimed,
        CancellationToken stoppingToken)
    {
        using var activity = PortalObservability.ActivitySource.StartActivity(
            "BackgroundJob.Execute",
            ActivityKind.Consumer);
        activity?.SetTag("portal.job_id", claimed.Id.ToString("D"));
        activity?.SetTag("portal.job_type", claimed.JobType);
        activity?.SetTag("portal.company_id", claimed.CompanyId.ToString("D"));
        activity?.SetTag("portal.correlation_id", claimed.CorrelationKey);

        using var jobScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["JobId"] = claimed.Id,
            ["JobType"] = claimed.JobType,
            ["CompanyId"] = claimed.CompanyId,
            ["CorrelationId"] = claimed.CorrelationKey,
            ["Module"] = "BackgroundJobs",
            ["Operation"] = "Execute"
        });

        var executionTimer = Stopwatch.StartNew();
        _logger.LogInformation(
            "Background job started. {JobId} {JobType} {CompanyId} {AttemptCount}",
            claimed.Id,
            claimed.JobType,
            claimed.CompanyId,
            claimed.AttemptCount);

        if (!handlers.TryGetValue(claimed.JobType, out var handler))
        {
            _logger.LogError(
                "Background job has no registered handler. {ErrorCode} {JobId} {JobType}",
                PortalErrorCodes.BackgroundJobFailed,
                claimed.Id,
                claimed.JobType);
            var failed = store.Fail(
                claimed.CompanyId,
                claimed.Id,
                _workerId,
                DateTime.UtcNow,
                "missing_handler",
                $"No background job handler is registered for job type '{claimed.JobType}'.");
            runtimeEventPublisher.Publish(failed, BackgroundJobStatus.Failed, failed.LastResultJson, failed.ErrorMessage);
            return;
        }

        runtimeEventPublisher.Publish(claimed, BackgroundJobStatus.Running, claimed.LastResultJson, claimed.ErrorMessage);

        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeatTask = RunHeartbeatAsync(store, claimed, heartbeatCts.Token);

        try
        {
            var result = await handler.HandleAsync(claimed, stoppingToken);
            heartbeatCts.Cancel();
            await AwaitHeartbeatAsync(heartbeatTask);

            if (result.Succeeded)
            {
                var completed = store.Complete(claimed.CompanyId, claimed.Id, _workerId, DateTime.UtcNow, result.ResultJson);
                runtimeEventPublisher.Publish(
                    completed,
                    BackgroundJobStatus.Completed,
                    result.RuntimeResultJson ?? result.ResultJson,
                    completed.ErrorMessage);
                _logger.LogInformation(
                    "Background job completed. {JobId} {JobType} {CompanyId} {DurationMs} {Result}",
                    claimed.Id,
                    claimed.JobType,
                    claimed.CompanyId,
                    executionTimer.ElapsedMilliseconds,
                    BackgroundJobStatus.Completed);
                return;
            }

            var failed = store.Fail(
                claimed.CompanyId,
                claimed.Id,
                _workerId,
                DateTime.UtcNow,
                result.ErrorCode,
                result.ErrorMessage,
                result.RetryDelay,
                result.ResultJson);
            runtimeEventPublisher.Publish(
                failed,
                failed.Status,
                result.RuntimeResultJson ?? result.ResultJson,
                failed.ErrorMessage);
            _logger.LogWarning(
                "Background job did not complete successfully. {ErrorCode} {JobId} {JobType} {CompanyId} {DurationMs} {Result}",
                result.ErrorCode ?? PortalErrorCodes.BackgroundJobFailed,
                claimed.Id,
                claimed.JobType,
                claimed.CompanyId,
                executionTimer.ElapsedMilliseconds,
                failed.Status);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            heartbeatCts.Cancel();
            await AwaitHeartbeatAsync(heartbeatTask);
            throw;
        }
        catch (Exception ex)
        {
            heartbeatCts.Cancel();
            await AwaitHeartbeatAsync(heartbeatTask);

            _logger.LogError(
                ex,
                "Background job failed with an unhandled exception. {ErrorCode} {JobId} {JobType} {CompanyId} {DurationMs}",
                PortalErrorCodes.BackgroundJobFailed,
                claimed.Id,
                claimed.JobType,
                claimed.CompanyId,
                executionTimer.ElapsedMilliseconds);
            var failed = store.Fail(
                claimed.CompanyId,
                claimed.Id,
                _workerId,
                DateTime.UtcNow,
                "unhandled_exception",
                IntegrationLogSanitizer.Diagnostic(ex.Message),
                DefaultErrorDelay);
            runtimeEventPublisher.Publish(failed, failed.Status, failed.LastResultJson, failed.ErrorMessage);
        }
    }

    private async Task RunHeartbeatAsync(IBackgroundJobStore store, BackgroundJobSnapshot job, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                store.Heartbeat(job.CompanyId, job.Id, _workerId, DateTime.UtcNow, TimeSpan.FromSeconds(45));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task AwaitHeartbeatAsync(Task heartbeatTask)
    {
        try
        {
            await heartbeatTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CleanupCompletedLanes()
    {
        foreach (var pair in _activeCompanyLanes.ToArray())
        {
            if (!pair.Value.IsCompleted)
            {
                continue;
            }

            _activeCompanyLanes.TryRemove(pair.Key, out _);
        }
    }
}

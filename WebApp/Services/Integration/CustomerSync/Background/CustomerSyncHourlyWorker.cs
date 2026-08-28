using WebApp.Models.Integration.CustomerSync;
using WebApp.Services.Integration.CustomerSync.Application;
using WebApp.Services.Integration.CustomerSync.Domain;
using WebApp.Services.Integration.CustomerSync;
using Microsoft.Extensions.Options;

namespace WebApp.Services.Integration.CustomerSync.Background;

// Periodically queues Jeeves-to-HubSpot customer sync jobs for enabled companies.
public sealed class CustomerSyncHourlyWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<CustomerSyncOptions> _baseOptions;
    private readonly ILogger<CustomerSyncHourlyWorker> _logger;

    public CustomerSyncHourlyWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<CustomerSyncOptions> baseOptions,
        ILogger<CustomerSyncHourlyWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _baseOptions = baseOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = await GetOptionsAsync(stoppingToken);

            var delay = TimeSpan.FromMinutes(Math.Max(5, options.PollIntervalMinutes));

            if (options.Enabled)
            {
                try
                {
                    await QueueEnabledCompaniesAsync(options, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "CustomerSync failed to queue scheduled runs.");
                }
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task<CustomerSyncOptions> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var baseOptions = _baseOptions.CurrentValue;
        if (!baseOptions.Enabled)
            return baseOptions;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var runtimeService = scope.ServiceProvider.GetRequiredService<ICustomerSyncRuntimeConfigurationService>();
            return await runtimeService.GetEffectiveOptionsAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "CustomerSync could not load runtime configuration. Falling back to app settings.");
            return baseOptions;
        }
    }

    private async Task QueueEnabledCompaniesAsync(CustomerSyncOptions options, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<CustomerSyncJobScheduler>();

        var queuedCount = 0;
        foreach (var company in CustomerSyncCompanyCatalog.GetUniqueCompanyOptions(options).Where(item => item.Enabled))
        {
            if (company.CompanyId == Guid.Empty || company.JeevesCompanyCode <= 0)
            {
                _logger.LogWarning("CustomerSync skipped company config with missing company id or Jeeves company code.");
                continue;
            }

            scheduler.EnqueueJeevesToHubSpotIfMissing(
                company.CompanyId,
                company.JeevesCompanyCode,
                CustomerSyncTrigger.Scheduled,
                DateTime.UtcNow);
            queuedCount++;
        }

        if (queuedCount > 0)
            _logger.LogInformation("CustomerSync queued {QueuedCount} companies for scheduled run.", queuedCount);
    }
}

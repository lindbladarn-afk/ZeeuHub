using System.Text.Json;
using Microsoft.Extensions.Options;
using WebApp.Models.Integration.CustomerSync;
using WebApp.Services.Integration.CustomerSync.Background;
using WebApp.Services.Integration.CustomerSync.Domain;
using WebApp.Services.Integration.CustomerSync.Persistence;
using WebApp.Services.Integration.CustomerSync;

namespace WebApp.Services.Integration.CustomerSync.Application;

// Resolves the effective CustomerSync runtime and persists hub-managed overrides.
public sealed class CustomerSyncRuntimeConfigurationService : ICustomerSyncRuntimeConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IOptionsMonitor<CustomerSyncOptions> _baseOptions;
    private readonly ICustomerSyncRuntimeConfigurationRepository _repository;
    private readonly CustomerSyncJobScheduler _scheduler;
    private readonly ILogger<CustomerSyncRuntimeConfigurationService> _logger;

    public CustomerSyncRuntimeConfigurationService(
        IOptionsMonitor<CustomerSyncOptions> baseOptions,
        ICustomerSyncRuntimeConfigurationRepository repository,
        CustomerSyncJobScheduler scheduler,
        ILogger<CustomerSyncRuntimeConfigurationService> logger)
    {
        _baseOptions = baseOptions;
        _repository = repository;
        _scheduler = scheduler;
        _logger = logger;
    }

    public async Task<CustomerSyncOptions> GetEffectiveOptionsAsync(CancellationToken cancellationToken = default)
    {
        var baseOptions = Clone(_baseOptions.CurrentValue);
        var runtime = await TryGetRuntimeConfigurationOrNullAsync(cancellationToken);
        if (runtime is null)
            return baseOptions;

        baseOptions.Enabled = runtime.Enabled;
        baseOptions.PollIntervalMinutes = runtime.PollIntervalMinutes;
        baseOptions.BatchSize = runtime.BatchSize;
        baseOptions.MaxAttempts = runtime.MaxAttempts;
        baseOptions.WebhookToleranceMinutes = runtime.WebhookToleranceMinutes;

        var companiesById = baseOptions.Companies.ToDictionary(item => item.CompanyId, item => item);
        foreach (var runtimeCompany in runtime.Companies)
        {
            if (runtimeCompany.CompanyId == Guid.Empty)
                continue;

            if (!companiesById.TryGetValue(runtimeCompany.CompanyId, out var company))
            {
                company = new CustomerSyncCompanyOptions
                {
                    CompanyId = runtimeCompany.CompanyId,
                    HubSpot = new CustomerSyncHubSpotOptions()
                };
                baseOptions.Companies.Add(company);
                companiesById[runtimeCompany.CompanyId] = company;
            }

            company.JeevesCompanyCode = runtimeCompany.JeevesCompanyCode;
            company.Enabled = runtimeCompany.Enabled;
            company.HubSpot.BaseUrl = runtimeCompany.HubSpotBaseUrl;
        }

        return baseOptions;
    }

    public async Task<CustomerSyncRuntimeConfiguration> GetRuntimeConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var runtime = await TryGetRuntimeConfigurationOrNullAsync(cancellationToken);
        return runtime ?? ProjectRuntimeConfiguration(_baseOptions.CurrentValue);
    }

    public async Task SaveRuntimeConfigurationAsync(CustomerSyncRuntimeConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var normalized = Normalize(configuration);
        var record = new CustomerSyncRuntimeConfigurationRecord
        {
            ConfigurationJson = JsonSerializer.Serialize(normalized, JsonOptions)
        };

        await _repository.UpsertAsync(record, cancellationToken);
    }

    public async Task<int> QueueManualRunsAsync(CancellationToken cancellationToken = default)
    {
        var effectiveOptions = await GetEffectiveOptionsAsync(cancellationToken);
        if (!effectiveOptions.Enabled)
            return 0;

        var utcNow = DateTime.UtcNow;
        var enqueuedCount = 0;

        foreach (var company in CustomerSyncCompanyCatalog.GetUniqueCompanyOptions(effectiveOptions).Where(item => item.Enabled))
        {
            if (company.CompanyId == Guid.Empty || company.JeevesCompanyCode <= 0)
                continue;

            _scheduler.EnqueueJeevesToHubSpotIfMissing(
                company.CompanyId,
                company.JeevesCompanyCode,
                CustomerSyncTrigger.Manual,
                utcNow);
            enqueuedCount++;
        }

        return enqueuedCount;
    }

    private async Task<CustomerSyncRuntimeConfiguration?> GetRuntimeConfigurationOrNullAsync(CancellationToken cancellationToken)
    {
        var record = await _repository.GetAsync(cancellationToken);
        if (record is null || string.IsNullOrWhiteSpace(record.ConfigurationJson))
            return null;

        return JsonSerializer.Deserialize<CustomerSyncRuntimeConfiguration>(record.ConfigurationJson, JsonOptions);
    }

    private async Task<CustomerSyncRuntimeConfiguration?> TryGetRuntimeConfigurationOrNullAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await GetRuntimeConfigurationOrNullAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "CustomerSync runtime configuration could not be loaded. Falling back to app settings.");
            return null;
        }
    }

    private static CustomerSyncRuntimeConfiguration ProjectRuntimeConfiguration(CustomerSyncOptions options)
    {
        return new CustomerSyncRuntimeConfiguration
        {
            Enabled = options.Enabled,
            PollIntervalMinutes = options.PollIntervalMinutes,
            BatchSize = options.BatchSize,
            MaxAttempts = options.MaxAttempts,
            WebhookToleranceMinutes = options.WebhookToleranceMinutes,
            Companies = CustomerSyncCompanyCatalog.GetUniqueCompanyOptions(options).Select(item => new CustomerSyncRuntimeCompanyConfiguration
            {
                CompanyId = item.CompanyId,
                JeevesCompanyCode = item.JeevesCompanyCode,
                Enabled = item.Enabled,
                HubSpotBaseUrl = item.HubSpot.BaseUrl
            }).ToList()
        };
    }

    private static CustomerSyncRuntimeConfiguration Normalize(CustomerSyncRuntimeConfiguration configuration)
    {
        var invalidCompany = configuration.Companies.FirstOrDefault(item => item.CompanyId == Guid.Empty || item.JeevesCompanyCode <= 0);
        if (invalidCompany is not null)
            throw new InvalidOperationException("Each CustomerSync company must have a valid company id and Jeeves company code.");

        var companies = configuration.Companies
            .Where(item => item.CompanyId != Guid.Empty)
            .Select(item => new CustomerSyncRuntimeCompanyConfiguration
            {
                CompanyId = item.CompanyId,
                JeevesCompanyCode = item.JeevesCompanyCode,
                Enabled = item.Enabled,
                HubSpotBaseUrl = string.IsNullOrWhiteSpace(item.HubSpotBaseUrl) ? null : item.HubSpotBaseUrl.Trim()
            })
            .ToList();

        return new CustomerSyncRuntimeConfiguration
        {
            Enabled = configuration.Enabled,
            PollIntervalMinutes = Math.Max(1, configuration.PollIntervalMinutes),
            BatchSize = Math.Max(1, configuration.BatchSize),
            MaxAttempts = Math.Max(1, configuration.MaxAttempts),
            WebhookToleranceMinutes = Math.Max(0, configuration.WebhookToleranceMinutes),
            Companies = companies
        };
    }

    private static CustomerSyncOptions Clone(CustomerSyncOptions source)
    {
        return new CustomerSyncOptions
        {
            Enabled = source.Enabled,
            PollIntervalMinutes = source.PollIntervalMinutes,
            BatchSize = source.BatchSize,
            MaxAttempts = source.MaxAttempts,
            WebhookToleranceMinutes = source.WebhookToleranceMinutes,
            Companies = CustomerSyncCompanyCatalog.GetUniqueCompanyOptions(source).Select(CloneCompany).ToList(),
            NamedCompanies = source.NamedCompanies.ToDictionary(
                item => item.Key,
                item => CloneCompany(item.Value),
                StringComparer.OrdinalIgnoreCase)
        };
    }

    private static CustomerSyncCompanyOptions CloneCompany(CustomerSyncCompanyOptions item)
        => new()
        {
            CompanyId = item.CompanyId,
            JeevesCompanyCode = item.JeevesCompanyCode,
            Enabled = item.Enabled,
            HubSpot = new CustomerSyncHubSpotOptions
            {
                BaseUrl = item.HubSpot.BaseUrl,
                Token = item.HubSpot.Token,
                WebhookSecret = item.HubSpot.WebhookSecret
            }
        };
}

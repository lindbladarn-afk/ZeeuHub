using WebApp.Models.BackgroundJobs;
using WebApp.Models.Integration.CustomerSync;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Services.Integration.CustomerSync.Application;
using WebApp.Services.Integration.CustomerSync.Background;
using WebApp.Services.Integration.CustomerSync.Domain;
using WebApp.Services.Integration.CustomerSync.Persistence;
using WebApp.ViewModels.Integration.CustomerSync;

namespace WebApp.Services.Integration.CustomerSync.Presentation;

// Builds the CustomerSync page model, including the latest non-secret runtime result per configured company.
public sealed class CustomerSyncPagePresenter
{
    private const int RecentJobLimit = 10;
    private const int ImportedCompanyPageSize = 25;
    private readonly CustomerSyncConfigurationPresenter _configurationPresenter;
    private readonly IBackgroundJobStore _backgroundJobStore;
    private readonly ICustomerSyncMappingRepository _mappingRepository;

    public CustomerSyncPagePresenter(
        CustomerSyncConfigurationPresenter configurationPresenter,
        IBackgroundJobStore backgroundJobStore,
        ICustomerSyncMappingRepository mappingRepository)
    {
        _configurationPresenter = configurationPresenter;
        _backgroundJobStore = backgroundJobStore;
        _mappingRepository = mappingRepository;
    }

    public CustomerSyncPageViewModel Build(CustomerSyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var model = _configurationPresenter.Build(options);
        var companies = model.Companies
            .Select(BuildCompanyWithLatestRun)
            .ToArray();

        return new CustomerSyncPageViewModel
        {
            IsEnabled = model.IsEnabled,
            PollIntervalMinutes = model.PollIntervalMinutes,
            BatchSize = model.BatchSize,
            MaxAttempts = model.MaxAttempts,
            Companies = companies
        };
    }

    public async Task<CustomerSyncPageViewModel> BuildAsync(CustomerSyncOptions options, int importedCompanyPage = 1, CancellationToken cancellationToken = default)
    {
        var model = Build(options);
        var companyIds = model.Companies.Select(item => item.CompanyId).Where(item => item != Guid.Empty).Distinct().ToArray();
        var importedCompanyTotalCount = await TryCountImportedCompaniesAsync(companyIds, cancellationToken);
        var safePage = NormalizePage(importedCompanyPage, importedCompanyTotalCount);
        var importedCompanies = await TryListImportedCompaniesAsync(companyIds, safePage, cancellationToken);

        return new CustomerSyncPageViewModel
        {
            IsEnabled = model.IsEnabled,
            PollIntervalMinutes = model.PollIntervalMinutes,
            BatchSize = model.BatchSize,
            MaxAttempts = model.MaxAttempts,
            Companies = model.Companies,
            ImportedHubSpotCompaniesPagination = new CustomerSyncImportedCompanyPaginationViewModel
            {
                Page = safePage,
                PageSize = ImportedCompanyPageSize,
                TotalCount = importedCompanyTotalCount
            },
            ImportedHubSpotCompanies = importedCompanies.Select(item => new CustomerSyncImportedCompanyViewModel
            {
                CompanyId = item.CompanyId,
                JeevesCompanyCode = item.JeevesCompanyCode,
                HubSpotCompanyId = item.HubSpotCompanyId ?? string.Empty,
                OrganizationNumber = item.OrganizationNumber,
                Name = item.NormalizedName,
                Domain = item.Domain,
                Email = item.Email,
                Phone = item.Phone,
                HubSpotUpdatedAtUtc = item.HubSpotUpdatedAtUtc,
                LastImportedAtUtc = item.LastSyncedFromHubSpotAtUtc ?? item.UpdatedAtUtc
            }).ToArray()
        };
    }

    private async Task<int> TryCountImportedCompaniesAsync(
        IReadOnlyCollection<Guid> companyIds,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _mappingRepository.CountHubSpotMappingsAsync(companyIds, cancellationToken);
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
    }

    private async Task<IReadOnlyList<CustomerSyncMappingRecord>> TryListImportedCompaniesAsync(
        IReadOnlyCollection<Guid> companyIds,
        int page,
        CancellationToken cancellationToken)
    {
        try
        {
            var skip = (page - 1) * ImportedCompanyPageSize;
            return await _mappingRepository.ListHubSpotMappingsAsync(companyIds, skip, ImportedCompanyPageSize, cancellationToken);
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            return Array.Empty<CustomerSyncMappingRecord>();
        }
    }

    private static int NormalizePage(int requestedPage, int totalCount)
    {
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)ImportedCompanyPageSize);
        return Math.Clamp(requestedPage, 1, totalPages);
    }

    private CustomerSyncCompanyStatusViewModel BuildCompanyWithLatestRun(CustomerSyncCompanyStatusViewModel company)
    {
        BackgroundJobSnapshot? latestJob = null;

        try
        {
            latestJob = _backgroundJobStore
                .ListRecent(company.CompanyId, RecentJobLimit)
                .Where(job => string.Equals(job.JobType, CustomerSyncBackgroundJobConstants.ExecuteJobType, StringComparison.Ordinal))
                .OrderByDescending(GetSortTimestamp)
                .FirstOrDefault();
        }
        catch
        {
            latestJob = null;
        }

        return new CustomerSyncCompanyStatusViewModel
        {
            DisplayName = company.DisplayName,
            CompanyId = company.CompanyId,
            JeevesCompanyCode = company.JeevesCompanyCode,
            Enabled = company.Enabled,
            HubSpotBaseUrl = company.HubSpotBaseUrl,
            HubSpotToken = company.HubSpotToken,
            LatestRun = latestJob is null ? null : BuildLatestRun(latestJob)
        };
    }

    private static CustomerSyncLatestRunViewModel BuildLatestRun(BackgroundJobSnapshot job)
    {
        var (statusLabel, statusTone) = job.Status switch
        {
            BackgroundJobStatus.Completed => ("Slutförd", "success"),
            BackgroundJobStatus.Failed => ("Misslyckad", "danger"),
            BackgroundJobStatus.Running => ("Pågår", "info"),
            BackgroundJobStatus.Queued => ("Köad", "info"),
            _ => ("Okänd", "muted")
        };

        var result = CustomerSyncResult.FromJson(job.LastResultJson);
        var summary = job.Status == BackgroundJobStatus.Failed
            ? job.ErrorMessage ?? "CustomerSync misslyckades."
            : !string.IsNullOrWhiteSpace(result.Summary)
                ? result.Summary
                : job.Status == BackgroundJobStatus.Completed
                    ? "CustomerSync är klar."
                    : "CustomerSync har uppdaterats.";

        return new CustomerSyncLatestRunViewModel
        {
            StatusLabel = statusLabel,
            StatusTone = statusTone,
            Summary = summary,
            CreatedCount = result.CreatedCount,
            UpdatedCount = result.UpdatedCount,
            SkippedCount = result.SkippedCount,
            FailedCount = result.FailedCount,
            DirectionLabel = TryGetDirectionLabel(job.PayloadJson),
            TriggerLabel = TryGetTriggerLabel(job.PayloadJson),
            FinishedAtUtc = job.CompletedAtUtc ?? job.StartedAtUtc ?? job.CreatedAtUtc
        };
    }

    private static DateTime GetSortTimestamp(BackgroundJobSnapshot job)
        => job.CompletedAtUtc ?? job.StartedAtUtc ?? job.CreatedAtUtc;

    private static string? TryGetDirectionLabel(string payloadJson)
    {
        try
        {
            var payload = CustomerSyncBackgroundJobPayload.FromJson(payloadJson);
            return payload.Direction switch
            {
                CustomerSyncDirection.JeevesToHubSpot => "Jeeves → HubSpot",
                CustomerSyncDirection.HubSpotToJeeves => "HubSpot → Jeeves",
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetTriggerLabel(string payloadJson)
    {
        try
        {
            var payload = CustomerSyncBackgroundJobPayload.FromJson(payloadJson);
            return payload.Trigger switch
            {
                CustomerSyncTrigger.Scheduled => "Schemalagd",
                CustomerSyncTrigger.Webhook => "Webhook",
                CustomerSyncTrigger.Manual => "Manuell",
                CustomerSyncTrigger.Replay => "Omspelning",
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}

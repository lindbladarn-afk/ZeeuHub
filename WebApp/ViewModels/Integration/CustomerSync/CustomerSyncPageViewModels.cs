namespace WebApp.ViewModels.Integration.CustomerSync;

// Presents the CustomerSync config state without exposing any secret values.
public sealed class CustomerSyncPageViewModel
{
    public bool IsEnabled { get; init; }
    public int PollIntervalMinutes { get; init; }
    public int BatchSize { get; init; }
    public int MaxAttempts { get; init; }
    public IReadOnlyList<CustomerSyncCompanyStatusViewModel> Companies { get; init; } = Array.Empty<CustomerSyncCompanyStatusViewModel>();
    public IReadOnlyList<CustomerSyncImportedCompanyViewModel> ImportedHubSpotCompanies { get; init; } = Array.Empty<CustomerSyncImportedCompanyViewModel>();
    public CustomerSyncImportedCompanyPaginationViewModel ImportedHubSpotCompaniesPagination { get; init; } = new();
}

public sealed class CustomerSyncCompanyStatusViewModel
{
    public string DisplayName { get; init; } = string.Empty;
    public Guid CompanyId { get; init; }
    public int JeevesCompanyCode { get; init; }
    public bool Enabled { get; init; }
    public bool HasHubSpotConnection { get; init; }
    public string HubSpotConnectionLabel { get; init; } = string.Empty;
    public string HubSpotConnectionTone { get; init; } = "danger";
    public string? HubSpotBaseUrl { get; init; }
    public CustomerSyncHubSpotTokenViewModel HubSpotToken { get; init; } = new();
    public CustomerSyncLatestRunViewModel? LatestRun { get; init; }
}

public sealed class CustomerSyncHubSpotTokenViewModel
{
    public bool IsConfigured { get; init; }
    public string StatusLabel { get; init; } = "Missing";
    public string StatusTone { get; init; } = "danger";
    public string? SecretName { get; init; }
    public string? SourceLabel { get; init; }
}

public sealed class CustomerSyncLatestRunViewModel
{
    public string StatusLabel { get; init; } = "Unknown";
    public string StatusTone { get; init; } = "muted";
    public string Summary { get; init; } = "Inga körningar ännu.";
    public int CreatedCount { get; init; }
    public int UpdatedCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }
    public string? DirectionLabel { get; init; }
    public string? TriggerLabel { get; init; }
    public DateTime? FinishedAtUtc { get; init; }
}

public sealed class CustomerSyncImportedCompanyViewModel
{
    public Guid CompanyId { get; init; }
    public int JeevesCompanyCode { get; init; }
    public string HubSpotCompanyId { get; init; } = string.Empty;
    public string? OrganizationNumber { get; init; }
    public string? Name { get; init; }
    public string? Domain { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public DateTime? HubSpotUpdatedAtUtc { get; init; }
    public DateTime? LastImportedAtUtc { get; init; }
}

public sealed class CustomerSyncImportedCompanyPaginationViewModel
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public int TotalCount { get; init; }
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => TotalPages > 0 && Page < TotalPages;
}

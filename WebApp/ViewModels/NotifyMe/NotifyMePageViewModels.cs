namespace WebApp.ViewModels.NotifyMe;

// Page-level NotifyMe models for overview, history, editor, library and statistics.
public sealed class NotifyMeOverviewVm
{
    public bool IsInstalled { get; set; }
    public string? StatusMessage { get; set; }
    public int? CompanyCode { get; set; }
    public int TotalNotifications { get; set; }
    public int ActiveNotifications { get; set; }
    public int DueNowCount { get; set; }
    public int EscalationConfiguredCount { get; set; }
    public int FilteredNotificationsCount { get; set; }
    public NotifyMePaginationVm Pagination { get; set; } = new();
    public IReadOnlyList<NotifyMeListItemVm> Notifications { get; set; } = Array.Empty<NotifyMeListItemVm>();
    public IReadOnlyList<NotifyMeLogItemVm> RecentLogEntries { get; set; } = Array.Empty<NotifyMeLogItemVm>();
    public NotifyMeOverviewFiltersVm Filters { get; set; } = new();
}

public sealed class NotifyMeHistoryPageVm
{
    public bool IsInstalled { get; set; }
    public string? StatusMessage { get; set; }
    public int? CompanyCode { get; set; }
    public int TotalHistoryEntries { get; set; }
    public IReadOnlyList<NotifyMeLogItemVm> HistoryEntries { get; set; } = Array.Empty<NotifyMeLogItemVm>();
    public NotifyMePaginationVm Pagination { get; set; } = new();
    public NotifyMeOverviewFiltersVm Filters { get; set; } = new();
}

public sealed class NotifyMeDetailsPageVm
{
    public bool IsInstalled { get; set; }
    public string? StatusMessage { get; set; }
    public int? CompanyCode { get; set; }
    public string? DefaultTestRecipientEmail { get; set; }
    public NotifyMeDetailsVm? Notification { get; set; }
}

public sealed class NotifyMeCreatePrototypeVm
{
    public int? NotificationId { get; set; }
    public int? CompanyCode { get; set; }
    public bool IsInstalled { get; set; }
    public bool IsEditMode { get; set; }
    public string? TemplateKey { get; set; }
    public string? TemplateName { get; set; }
    public string? StatusMessage { get; set; }
    public IReadOnlyList<NotifyMeLookupOptionVm> Types { get; set; } = Array.Empty<NotifyMeLookupOptionVm>();
    public IReadOnlyList<NotifyMeLookupOptionVm> Priorities { get; set; } = Array.Empty<NotifyMeLookupOptionVm>();
    public IReadOnlyList<NotifyMeLookupOptionVm> Schemas { get; set; } = Array.Empty<NotifyMeLookupOptionVm>();
    public IReadOnlyList<NotifyMeLookupOptionVm> Schedules { get; set; } = Array.Empty<NotifyMeLookupOptionVm>();
    public NotifyMeDraftVm Draft { get; set; } = new();
}

public sealed class NotifyMeTemplateLibraryVm
{
    public bool IsInstalled { get; set; }
    public string? StatusMessage { get; set; }
    public int? CompanyCode { get; set; }
    public string? Search { get; set; }
    public string? Category { get; set; }
    public int TotalTemplates { get; set; }
    public IReadOnlyList<NotifyMeLookupOptionVm> CategoryOptions { get; set; } = Array.Empty<NotifyMeLookupOptionVm>();
    public IReadOnlyList<NotifyMeTemplateVm> Templates { get; set; } = Array.Empty<NotifyMeTemplateVm>();
}

public sealed class NotifyMeStatisticsVm
{
    public bool IsInstalled { get; set; }
    public string? StatusMessage { get; set; }
    public int? CompanyCode { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public int TotalRuns { get; set; }
    public int TotalHits { get; set; }
    public decimal HitRatePercent { get; set; }
    public decimal EstimatedHoursSaved { get; set; }
    public decimal EstimatedValueProtectedSek { get; set; }
    public IReadOnlyList<NotifyMeStatsPointVm> Trend { get; set; } = Array.Empty<NotifyMeStatsPointVm>();
    public IReadOnlyList<NotifyMeNotificationStatsRowVm> NotificationRows { get; set; } = Array.Empty<NotifyMeNotificationStatsRowVm>();
    public IReadOnlyList<NotifyMeStatsInsightVm> Insights { get; set; } = Array.Empty<NotifyMeStatsInsightVm>();
}

public sealed class NotifyMeTestRunResultVm
{
    public int NotificationId { get; set; }
    public int CompanyCode { get; set; }
    public string OverrideRecipient { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public DateTime? LoggedAt { get; set; }
    public bool LogCreated { get; set; }
    public bool MailQueued { get; set; }
    public long? MailItemId { get; set; }
    public string? MailStatus { get; set; }
}

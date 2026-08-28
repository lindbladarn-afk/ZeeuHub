namespace WebApp.ViewModels.NotifyMe;

// Shared NotifyMe support models used across pages, lists and statistics.
public sealed class NotifyMeListItemVm
{
    public int NotificationId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string WarningText { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string PriorityLabel { get; set; } = string.Empty;
    public string? SchemaCode { get; set; }
    public string? ScheduleCode { get; set; }
    public string ScheduleLabel { get; set; } = string.Empty;
    public bool HasAutomation { get; set; }
    public string? AutomationHint { get; set; }
    public bool IsActive { get; set; }
    public DateTime? NextExecutionAt { get; set; }
    public string NextExecutionDisplay { get; set; } = "-";
    public DateTime? LastWarningAt { get; set; }
    public int WarningCount { get; set; }
    public short? EscalateAfterCount { get; set; }
    public bool IsDueNow { get; set; }
    public string? LatestExecutionStatus { get; set; }
    public string LatestExecutionStatusTone { get; set; } = "secondary";
    public DateTime? LatestExecutionAt { get; set; }
    public string? LatestExecutionSummary { get; set; }
}

public sealed class NotifyMeDetailsVm
{
    public int NotificationId { get; set; }
    public int CompanyCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public string WarningText { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public string TypeCode { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string PriorityCode { get; set; } = string.Empty;
    public string PriorityLabel { get; set; } = string.Empty;
    public string? PrimaryEmail { get; set; }
    public string? SecondaryEmail { get; set; }
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public string? SchemaCode { get; set; }
    public string? ScheduleCode { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? NextExecutionAt { get; set; }
    public DateTime? LastWarningAt { get; set; }
    public int WarningCount { get; set; }
    public short? EscalateAfterCount { get; set; }
    public string? EscalationEmail { get; set; }
    public bool IsActive { get; set; }
    public bool UsesSysChangeSource { get; set; }
    public bool UsesCustomSql { get; set; }
    public bool UsesDynamicRecipients { get; set; }
    public string? SqlPreview { get; set; }
    public string? SysChangeSource { get; set; }
    public IReadOnlyList<NotifyMeLogItemVm> RecentLogEntries { get; set; } = Array.Empty<NotifyMeLogItemVm>();
}

public sealed class NotifyMeLogItemVm
{
    public int LogId { get; set; }
    public int NotificationId { get; set; }
    public string NotificationDescription { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string ExecutionStatus { get; set; } = "Skickad";
    public string ExecutionStatusTone { get; set; } = "success";
    public string Recipients { get; set; } = string.Empty;
    public string SchemaCode { get; set; } = string.Empty;
    public string HtmlPreviewText { get; set; } = string.Empty;
}

public sealed class NotifyMeOverviewFiltersVm
{
    public string? Search { get; set; }
    public string Status { get; set; } = "all";
    public string? Type { get; set; }
    public string? Priority { get; set; }
    public int? HistoryNotificationId { get; set; }
    public string? HistorySearch { get; set; }
    public IReadOnlyList<NotifyMeLookupOptionVm> TypeOptions { get; set; } = Array.Empty<NotifyMeLookupOptionVm>();
    public IReadOnlyList<NotifyMeLookupOptionVm> PriorityOptions { get; set; } = Array.Empty<NotifyMeLookupOptionVm>();
    public IReadOnlyList<NotifyMeLookupOptionVm> HistoryNotificationOptions { get; set; } = Array.Empty<NotifyMeLookupOptionVm>();
}

public sealed class NotifyMeLookupOptionVm
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class NotifyMeDraftVm
{
    public int? NotificationId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string WarningText { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public string? TypeCode { get; set; }
    public string? PriorityCode { get; set; }
    public string? PrimaryEmail { get; set; }
    public string? SecondaryEmail { get; set; }
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public string? SchemaCode { get; set; }
    public string? ScheduleCode { get; set; }
    public DateTime? StartDate { get; set; }
    public short? EscalateAfterCount { get; set; }
    public string? EscalationEmail { get; set; }
    public string? SqlPreview { get; set; }
    public bool UsesDynamicRecipients { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class NotifyMePaginationVm
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public sealed class NotifyMeTemplateVm
{
    public string Key { get; set; } = string.Empty;
    public int? SourceNotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string BusinessValue { get; set; } = string.Empty;
    public string ExampleFrequency { get; set; } = string.Empty;
    public string SuggestedPriority { get; set; } = string.Empty;
    public string ComplexityLabel { get; set; } = string.Empty;
    public IReadOnlyList<string> ParameterHints { get; set; } = Array.Empty<string>();
    public NotifyMeDraftVm Draft { get; set; } = new();
}

public sealed class NotifyMeStatsPointVm
{
    public string Label { get; set; } = string.Empty;
    public int HitCount { get; set; }
    public int RunCount { get; set; }
    public int HeightPercent { get; set; }
}

public sealed class NotifyMeNotificationStatsRowVm
{
    public int NotificationId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int RunCount { get; set; }
    public int HitCount { get; set; }
    public decimal HitRatePercent { get; set; }
    public string QualityLabel { get; set; } = string.Empty;
    public string QualityTone { get; set; } = "secondary";
}

public sealed class NotifyMeStatsInsightVm
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tone { get; set; } = "info";
}

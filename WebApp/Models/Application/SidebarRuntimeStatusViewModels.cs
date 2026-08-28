namespace WebApp.Models.Application;

public sealed class SidebarRuntimeStatusViewModel
{
    public bool IsVisible { get; set; }
    public string OverallLabel { get; set; } = "Ready";
    public string OverallTone { get; set; } = "ready";
    public string RunningLabel { get; set; } = "Idle";
    public string LatestLabel { get; set; } = "Ingen aktivitet ännu";
    public SidebarRuntimeStatusItemViewModel? LatestItem { get; set; }
    public int NotificationCount { get; set; }
    public bool HasUnread => NotificationCount > 0;
    public SidebarRuntimeStatusItemViewModel? ActionCenterSummaryItem { get; set; }
    public IReadOnlyList<SidebarRuntimeStatusItemViewModel> Items { get; set; } = Array.Empty<SidebarRuntimeStatusItemViewModel>();
    public IReadOnlyList<SidebarRuntimeStatusItemViewModel> NotificationItems { get; set; } = Array.Empty<SidebarRuntimeStatusItemViewModel>();
}

// Carries the transient row-level details shown for completed Excel imports.
public sealed class ExcelImportRuntimeRowViewModel
{
    public int RowNo { get; set; }
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string> Cells { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SidebarRuntimeStatusItemViewModel
{
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string? AggregateKey { get; set; }
    public Guid? ImportBatchId { get; set; }
    public string? SourceFileName { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public int? TotalRows { get; set; }
    public int? ValidRows { get; set; }
    public int? InvalidRows { get; set; }
    public int? StagedRows { get; set; }
    public string? DurationLabel { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusTone { get; set; } = "muted";
    public string TimeLabel { get; set; } = string.Empty;
    public string IconClass { get; set; } = "fa fa-circle";
    public List<string> ColumnHeaders { get; set; } = new();
    public List<ExcelImportRuntimeRowViewModel> ImportedRows { get; set; } = new();
    public string? VoucherPostingDate { get; set; }
    public string? VoucherReversalDate { get; set; }
}

public sealed class SidebarRuntimeEventRecord
{
    public Guid CompanyId { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string? AggregateKey { get; set; }
    public Guid? ImportBatchId { get; set; }
    public string? SourceFileName { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public int? TotalRows { get; set; }
    public int? ValidRows { get; set; }
    public int? InvalidRows { get; set; }
    public int? StagedRows { get; set; }
    public string? DurationLabel { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusTone { get; set; } = "muted";
    public string IconClass { get; set; } = "fa fa-circle";
    public List<string> ColumnHeaders { get; set; } = new();
    public List<ExcelImportRuntimeRowViewModel> ImportedRows { get; set; } = new();
    public string? VoucherPostingDate { get; set; }
    public string? VoucherReversalDate { get; set; }
}

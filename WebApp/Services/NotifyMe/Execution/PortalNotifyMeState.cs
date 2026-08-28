namespace WebApp.Services.NotifyMe;

// Runtime snapshot for a single portal-owned NotifyMe rule.
public sealed class PortalNotifyMeState
{
    public int NotificationId { get; set; }
    public int CompanyCode { get; set; }
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
    public DateTime? NextExecutionAt { get; set; }
    public int WarningCount { get; set; }
    public int? EscalateAfterCount { get; set; }
    public string? EscalationEmail { get; set; }
    public string? SqlPreview { get; set; }
    public string? SysChangeSource { get; set; }
    public string? DynamicAddress { get; set; }
    public string? IsActiveCode { get; set; }
}

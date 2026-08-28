namespace WebApp.Services.Application.Infrastructure;

// Configures how long portal-side operational data is kept before cleanup.
public sealed class DataRetentionOptions
{
    public const string SectionName = "Retention";

    public bool Enabled { get; set; } = true;
    public int RunIntervalHours { get; set; } = 24;
    public int BackgroundJobsRetentionDays { get; set; } = 14;
    public int BackgroundJobRuntimeEventsRetentionDays { get; set; } = 7;
    public int PortalEventLogsRetentionDays { get; set; } = 14;
    public int AiQueryLogsRetentionDays { get; set; } = 14;
    public int ExcelImportLogsRetentionDays { get; set; } = 14;
    public int ExcelImportBackgroundFilesRetentionDays { get; set; } = 2;
    public int FlowEngineJobsRetentionDays { get; set; } = 90;
    public int ActionCenterItemStatesRetentionDays { get; set; } = 180;
    public int BankReconciliationUploadRetentionDays { get; set; } = 7;
    public bool PurgeExpiredAuthenticationTickets { get; set; } = true;
}

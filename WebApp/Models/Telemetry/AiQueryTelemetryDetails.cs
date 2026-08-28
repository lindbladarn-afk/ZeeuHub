// Carries structured production diagnostics for an Intelligence telemetry entry.
namespace WebApp.Models.Telemetry;

public sealed class AiQueryTelemetryDetails
{
    public Guid? ResponseId { get; set; }
    public string? PromptVersion { get; set; }
    public string? ModelDeployment { get; set; }
    public string? ErrorCode { get; set; }
    public string? VerificationStatus { get; set; }
    public long? DurationMs { get; set; }
    public long? PlanningDurationMs { get; set; }
    public long? SqlDurationMs { get; set; }
    public long? SummaryDurationMs { get; set; }
    public int? ModelRetryCount { get; set; }
    public int? RowCount { get; set; }
    public bool? WasTruncated { get; set; }
}

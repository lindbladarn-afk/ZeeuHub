using System.ComponentModel.DataAnnotations;

namespace WebApp.Models.Telemetry;

public class AiQueryLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? CompanyId { get; set; }
    public string? UserId { get; set; }
    public string? Question { get; set; }
    public bool WasAllowed { get; set; }
    public bool? WasSuccessful { get; set; }
    public string? SqlText { get; set; }
    public string? ErrorMessage { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
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
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

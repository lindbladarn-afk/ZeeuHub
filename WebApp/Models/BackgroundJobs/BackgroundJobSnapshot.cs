namespace WebApp.Models.BackgroundJobs;

public sealed class BackgroundJobSnapshot
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? CreatedByEmail { get; set; }
    public string JobType { get; set; } = string.Empty;
    public BackgroundJobStatus Status { get; set; }
    public string? CorrelationKey { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime QueuedAtUtc { get; set; }
    public DateTime AvailableAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public string? ClaimedBy { get; set; }
    public DateTime? ClaimedAtUtc { get; set; }
    public DateTime? LastHeartbeatAtUtc { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? LastResultJson { get; set; }
}

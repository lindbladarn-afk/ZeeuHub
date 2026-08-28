namespace WebApp.Models.BackgroundJobs;

public sealed class BackgroundJobEnqueueRequest
{
    public Guid CompanyId { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? CreatedByEmail { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string? CorrelationKey { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public int MaxAttempts { get; set; } = 3;
    public DateTime? AvailableAtUtc { get; set; }
}

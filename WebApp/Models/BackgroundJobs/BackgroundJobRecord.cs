using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.BackgroundJobs;

public sealed class BackgroundJobRecord
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CompanyId { get; set; }

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    [MaxLength(256)]
    public string? CreatedByEmail { get; set; }

    [MaxLength(128)]
    public string JobType { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Status { get; set; } = BackgroundJobStatus.Queued.ToString();

    [MaxLength(128)]
    public string? CorrelationKey { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string PayloadJson { get; set; } = "{}";

    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 3;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime QueuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime AvailableAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    [MaxLength(128)]
    public string? ClaimedBy { get; set; }
    public DateTime? ClaimedAtUtc { get; set; }
    public DateTime? LastHeartbeatAtUtc { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    [MaxLength(64)]
    public string? ErrorCode { get; set; }

    [MaxLength(4000)]
    [Column(TypeName = "nvarchar(4000)")]
    public string? ErrorMessage { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? LastResultJson { get; set; }
}

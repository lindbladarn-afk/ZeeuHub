using System.ComponentModel.DataAnnotations;

namespace WebApp.Models.Telemetry;

public class UserUsageTotal
{
    [Key]
    public string UserId { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public int TotalMinutes { get; set; }
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

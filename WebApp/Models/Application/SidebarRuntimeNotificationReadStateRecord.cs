using System.ComponentModel.DataAnnotations;

namespace WebApp.Models.Application;

public sealed class SidebarRuntimeNotificationReadStateRecord
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public DateTime LastReadAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

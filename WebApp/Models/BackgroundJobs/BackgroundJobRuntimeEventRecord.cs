using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.BackgroundJobs;

public sealed class BackgroundJobRuntimeEventRecord
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? JobId { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    [MaxLength(64)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? AggregateKey { get; set; }

    [MaxLength(64)]
    public string Source { get; set; } = string.Empty;

    [MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(64)]
    public string StatusLabel { get; set; } = string.Empty;

    [MaxLength(32)]
    public string StatusTone { get; set; } = "muted";

    [MaxLength(64)]
    public string IconClass { get; set; } = "fa fa-circle";

    [MaxLength(1024)]
    public string? LinkUrl { get; set; }

    [MaxLength(4000)]
    [Column(TypeName = "nvarchar(4000)")]
    public string Summary { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

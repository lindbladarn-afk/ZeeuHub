using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.Integration;

public class FlowEngineJobRecord
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CompanyId { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    [MaxLength(256)]
    public string? UserName { get; set; }

    [MaxLength(128)]
    public string? Name { get; set; }

    [MaxLength(128)]
    public string? UiLabel { get; set; }

    public bool IsScheduled { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = FlowEngineJobStatus.Queued.ToString();

    [Column(TypeName = "nvarchar(max)")]
    public string ArgumentsJson { get; set; } = "[]";

    [Column(TypeName = "nvarchar(max)")]
    public string? RequestJson { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }

    [MaxLength(512)]
    public string? ResultCommandLine { get; set; }

    public int? ResultExitCode { get; set; }
    public bool? ResultSucceeded { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ResultStandardOutput { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ResultStandardError { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ErrorMessage { get; set; }
}

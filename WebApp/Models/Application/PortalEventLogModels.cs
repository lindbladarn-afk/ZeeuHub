using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.Application;

public sealed class PortalEventLogRecord
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(128)]
    public string Module { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Action { get; set; } = string.Empty;

    public Guid? CompanyId { get; set; }

    [MaxLength(256)]
    public string? CompanyName { get; set; }

    public int? JeevesCompanyCode { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    [MaxLength(256)]
    public string? UserEmail { get; set; }

    [MaxLength(512)]
    public string? RequestPath { get; set; }

    [MaxLength(128)]
    public string? CorrelationId { get; set; }

    [MaxLength(32)]
    public string Severity { get; set; } = "Error";

    [MaxLength(4000)]
    [Column(TypeName = "nvarchar(4000)")]
    public string Message { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string? Exception { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? AdditionalData { get; set; }
}

public sealed class PortalEventLogEntry
{
    public DateTime? OccurredAtUtc { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public int? JeevesCompanyCode { get; set; }
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? RequestPath { get; set; }
    public string? CorrelationId { get; set; }
    public string Severity { get; set; } = "Error";
    public string Message { get; set; } = string.Empty;
    public string? AdditionalData { get; set; }
    public Exception? Exception { get; set; }
}

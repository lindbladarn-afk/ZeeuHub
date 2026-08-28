using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.AI;

/// <summary>
/// Persists portal-managed AI quota policy values.
/// Supports one global policy row and optional per-company overrides.
/// </summary>
public sealed class AiQuotaPolicy
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public bool IsGlobal { get; set; }

    [Column(TypeName = "uniqueidentifier")]
    public Guid? CompanyId { get; set; }

    public bool? Enabled { get; set; }
    public int? FreeTokensPerPeriod { get; set; }
    public int? WarningThresholdPercent { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    [Column(TypeName = "nvarchar(450)")]
    public string? UpdatedByUserId { get; set; }
}

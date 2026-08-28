using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.ActionCenter;

public enum ActionCenterItemStatus
{
    Active = 0,
    Completed = 1,
    Dismissed = 2
}

/// <summary>
/// Per-user/company status for en genererad insikt (identifierad via ExternalId).
/// </summary>
[Table("ActionCenterItemStates", Schema = "Identity")]
public class ActionCenterItemState
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column(TypeName = "nvarchar(256)")]
    public string ExternalId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(64)")]
    public ActionCenterItemStatus Status { get; set; } = ActionCenterItemStatus.Active;

    public Guid? CompanyId { get; set; }

    [Column(TypeName = "nvarchar(450)")]
    public string UserId { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "nvarchar(256)")]
    public string? Title { get; set; }

    [Column(TypeName = "nvarchar(1024)")]
    public string? Description { get; set; }

    [Column(TypeName = "nvarchar(64)")]
    public string? Category { get; set; }

    public ActionCenterPriority? Priority { get; set; }

    public DateTime? DetectedAtUtc { get; set; }

    [Column(TypeName = "nvarchar(512)")]
    public string? Comment { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
}

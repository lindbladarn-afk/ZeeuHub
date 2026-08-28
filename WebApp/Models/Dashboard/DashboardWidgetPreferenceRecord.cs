// Stores a user's presentation-only dashboard choices for one company.
using System.ComponentModel.DataAnnotations;

namespace WebApp.Models.Dashboard;

public sealed class DashboardWidgetPreferenceRecord
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [MaxLength(64)]
    public string WidgetId { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    [Required]
    [MaxLength(16)]
    public string Size { get; set; } = DashboardWidgetSize.Compact.ToString();

    public bool IsVisible { get; set; } = true;

    [Required]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

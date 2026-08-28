// Defines the validated client contract used when a user saves a personal dashboard layout.
namespace WebApp.Models.Dashboard;

public sealed class DashboardLayoutUpdateRequest
{
    public IReadOnlyList<DashboardWidgetLayoutUpdateItem>? Widgets { get; init; }
}

public sealed class DashboardWidgetLayoutUpdateItem
{
    public string? WidgetId { get; init; }
    public int SortOrder { get; init; }
    public DashboardWidgetSize Size { get; init; }
}

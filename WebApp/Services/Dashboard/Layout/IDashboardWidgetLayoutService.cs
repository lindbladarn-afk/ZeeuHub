// Owns durable dashboard layout preferences without exposing presentation internals to clients.
using Entities.Application;
using WebApp.Models.Dashboard;

namespace WebApp.Services.Dashboard;

public interface IDashboardWidgetLayoutService
{
    Task<IReadOnlyList<DashboardWidgetLayout>> GetLayoutAsync(
        UserSession? user,
        IReadOnlyList<DashboardWidgetLayout> defaultLayout,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        UserSession user,
        IReadOnlyList<DashboardWidgetLayout> widgets,
        IReadOnlyCollection<DashboardCardDefinition> allowedCards,
        CancellationToken cancellationToken = default);

    Task ResetAsync(UserSession user, CancellationToken cancellationToken = default);
}

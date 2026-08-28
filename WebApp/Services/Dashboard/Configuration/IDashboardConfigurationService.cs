// Provides the card catalog and default layout available to the active dashboard user.
using Entities.Application;
using WebApp.Models.Dashboard;

namespace WebApp.Services.Dashboard;

public interface IDashboardConfigurationService
{
    Task<IReadOnlyList<DashboardCardDefinition>> GetAvailableCardsAsync(UserSession? user, CancellationToken cancellationToken = default);
    IReadOnlyList<DashboardWidgetLayout> GetDefaultLayout(UserSession? user);
}

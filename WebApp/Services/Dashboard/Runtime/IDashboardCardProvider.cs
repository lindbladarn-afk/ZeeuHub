// Defines the isolated builder contract implemented by each dashboard card feature.
using WebApp.Models.Dashboard;

namespace WebApp.Services.Dashboard;

public interface IDashboardCardProvider
{
    IReadOnlyCollection<string> CardIds { get; }

    Task<DashboardCardViewModel?> BuildAsync(
        DashboardCardDefinition definition,
        DashboardCardBuildContext context,
        CancellationToken cancellationToken);
}

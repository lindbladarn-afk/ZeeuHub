// Provides full dashboard composition and isolated card refresh operations.
using WebApp.Models.Dashboard;

namespace WebApp.Services.Dashboard;

public interface IMemberDashboardService
{
    Task<MemberDashboardPageViewModel> BuildAsync(CancellationToken cancellationToken = default);
    Task<DashboardCardViewModel?> BuildCardAsync(string cardId, CancellationToken cancellationToken = default);
}

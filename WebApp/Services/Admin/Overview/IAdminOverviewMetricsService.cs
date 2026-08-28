using WebApp.ViewModels.Admin;

namespace WebApp.Services.Admin;

// Provides the high-level admin dashboard metrics shown on overview.
public interface IAdminOverviewMetricsService
{
    Task<AdminOverviewViewModel> GetOverviewAsync();
    void InvalidateCache();
}

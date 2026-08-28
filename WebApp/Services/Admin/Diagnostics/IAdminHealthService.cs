using WebApp.ViewModels.Admin;

namespace WebApp.Services.Admin;

// Provides platform-level health checks for admin diagnostics and internal operations signals.
public interface IAdminHealthService
{
    Task<List<AdminOverviewViewModel.HealthStatusItem>> GetHealthAsync();
    Task<AdminOverviewViewModel.HealthStatusItem> CheckSqlAsync(string name, string? connectionString);
    List<AdminOverviewViewModel.HealthStatusItem> GetHealthTemplates();
}

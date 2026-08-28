using WebApp.ViewModels.Admin;

namespace WebApp.Services.Admin;

public interface IAdminOverviewService
{
    Task<AdminOverviewViewModel> GetOverviewAsync();
    Task<List<AdminOverviewViewModel.HealthStatusItem>> GetHealthAsync();
    Task<AdminHealthDetailViewModel> GetCompanyConnectionHealthAsync();
}

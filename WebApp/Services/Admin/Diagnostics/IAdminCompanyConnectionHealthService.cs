using WebApp.ViewModels.Admin;

namespace WebApp.Services.Admin;

// Builds the detailed per-company connection health view for ZeeU admins.
public interface IAdminCompanyConnectionHealthService
{
    Task<AdminHealthDetailViewModel> GetCompanyConnectionHealthAsync();
}

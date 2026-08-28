using WebApp.ViewModels.Admin;

namespace WebApp.Services.Admin;

// This facade preserves the existing admin overview contract while delegating each responsibility
// to a smaller focused service: overview metrics, platform health, and company connection diagnostics.
public sealed class AdminOverviewService : IAdminOverviewService
{
    private readonly IAdminOverviewMetricsService _overviewMetricsService;
    private readonly IAdminHealthService _adminHealthService;
    private readonly IAdminCompanyConnectionHealthService _companyConnectionHealthService;

    public AdminOverviewService(
        IAdminOverviewMetricsService overviewMetricsService,
        IAdminHealthService adminHealthService,
        IAdminCompanyConnectionHealthService companyConnectionHealthService)
    {
        _overviewMetricsService = overviewMetricsService;
        _adminHealthService = adminHealthService;
        _companyConnectionHealthService = companyConnectionHealthService;
    }

    public Task<AdminOverviewViewModel> GetOverviewAsync() => _overviewMetricsService.GetOverviewAsync();
    public Task<List<AdminOverviewViewModel.HealthStatusItem>> GetHealthAsync() => _adminHealthService.GetHealthAsync();
    public Task<AdminHealthDetailViewModel> GetCompanyConnectionHealthAsync() => _companyConnectionHealthService.GetCompanyConnectionHealthAsync();
}

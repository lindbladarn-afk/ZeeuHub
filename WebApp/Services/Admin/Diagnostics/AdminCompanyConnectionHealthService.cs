using WebApp.Data;
using WebApp.Services.Application;
using WebApp.ViewModels.Admin;

namespace WebApp.Services.Admin;

// Builds the detailed company-by-company connection health report used in admin diagnostics.
public sealed class AdminCompanyConnectionHealthService : IAdminCompanyConnectionHealthService
{
    private readonly Repository.Contracts.IAdminCompanyRepository _adminCompanyRepository;
    private readonly IConnectionStringResolver _connectionStringResolver;
    private readonly ApplicationDbContext _context;
    private readonly IApplicationConnectionContextService _applicationConnectionContextService;
    private readonly IAdminHealthService _adminHealthService;

    public AdminCompanyConnectionHealthService(
        Repository.Contracts.IAdminCompanyRepository adminCompanyRepository,
        IConnectionStringResolver connectionStringResolver,
        ApplicationDbContext context,
        IApplicationConnectionContextService applicationConnectionContextService,
        IAdminHealthService adminHealthService)
    {
        _adminCompanyRepository = adminCompanyRepository;
        _connectionStringResolver = connectionStringResolver;
        _context = context;
        _applicationConnectionContextService = applicationConnectionContextService;
        _adminHealthService = adminHealthService;
    }

    public async Task<AdminHealthDetailViewModel> GetCompanyConnectionHealthAsync()
    {
        var result = new AdminHealthDetailViewModel();
        var companies = await _adminCompanyRepository.GetCompanies() ?? new List<Entities.ViewModels.Admin.ManageCompanyVM>();

        foreach (var company in companies)
        {
            var connStrings = await _applicationConnectionContextService.GetConnectionStringsAsync(_context, company.Id);
            foreach (var cs in connStrings)
            {
                var resolved = await _connectionStringResolver.ResolveAsync(connStrings, cs.Id, company.Id);
                if (!resolved.Success)
                {
                    result.Items.Add(new AdminHealthDetailViewModel.CompanyConnectionHealthItem
                    {
                        CompanyId = company.Id,
                        CompanyName = company.Name,
                        ConnectionId = cs.Id,
                        ConnectionName = cs.ConnectionStringType?.Name ?? "Okänd",
                        DatabaseName = cs.DatabaseName,
                        IsActive = cs.IsActive,
                        IsHealthy = false,
                        Message = resolved.Error ?? "Saknar connection string"
                    });
                    continue;
                }

                var health = await _adminHealthService.CheckSqlAsync($"{company.Name} ({cs.ConnectionStringType?.Name ?? "Connection"})", resolved.Value);
                result.Items.Add(new AdminHealthDetailViewModel.CompanyConnectionHealthItem
                {
                    CompanyId = company.Id,
                    CompanyName = company.Name,
                    ConnectionId = cs.Id,
                    ConnectionName = cs.ConnectionStringType?.Name ?? "Connection",
                    DatabaseName = cs.DatabaseName,
                    IsActive = cs.IsActive,
                    IsHealthy = health.IsHealthy,
                    Message = health.Description
                });
            }
        }

        return result;
    }
}

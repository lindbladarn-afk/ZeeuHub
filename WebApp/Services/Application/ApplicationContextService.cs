using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Identity;

namespace WebApp.Services.Application;

public class ApplicationContextService : IApplicationContextService
{
    private readonly IApplicationUserContextService _userContextService;
    private readonly IApplicationCompanyContextService _companyContextService;
    private readonly IApplicationConnectionContextService _connectionContextService;

    public ApplicationContextService(
        IApplicationUserContextService userContextService,
        IApplicationCompanyContextService companyContextService,
        IApplicationConnectionContextService connectionContextService)
    {
        _userContextService = userContextService;
        _companyContextService = companyContextService;
        _connectionContextService = connectionContextService;
    }

    public Task<ApplicationUser?> GetUserByEmailAsync(ApplicationDbContext context, string email)
    {
        return _userContextService.GetUserByEmailAsync(context, email);
    }

    public Task<ApplicationCompany?> GetCompanyAsync(ApplicationDbContext context, Guid companyId)
    {
        return _companyContextService.GetCompanyAsync(context, companyId);
    }

    public Task<List<ApplicationCompanyConnectionStrings>> GetConnectionStringsAsync(ApplicationDbContext context, Guid companyId)
    {
        return _connectionContextService.GetConnectionStringsAsync(context, companyId);
    }

    public Task<List<ApplicationCompanyLicense>> GetLicensesAsync(ApplicationDbContext context, Guid companyId)
    {
        return _companyContextService.GetLicensesAsync(context, companyId);
    }

    public Task<List<ApplicationCompanyPermission>> GetPermissionsAsync(ApplicationDbContext context, Guid companyId)
    {
        return _companyContextService.GetPermissionsAsync(context, companyId);
    }
}

using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Identity;

namespace WebApp.Services.Application;

public sealed class ApplicationCompanyContextService : IApplicationCompanyContextService
{
    public Task<ApplicationCompany?> GetCompanyAsync(ApplicationDbContext context, Guid companyId)
    {
        return context.Companies!.FirstOrDefaultAsync(x => x.Id == companyId);
    }

    public Task<List<ApplicationCompanyLicense>> GetLicensesAsync(ApplicationDbContext context, Guid companyId)
    {
        return context.Licenses!
            .Where(x => x.CompanyId == companyId)
            .ToListAsync();
    }

    public Task<List<ApplicationCompanyPermission>> GetPermissionsAsync(ApplicationDbContext context, Guid companyId)
    {
        return context.CompanyPermissions!
            .Where(x => x.CompanyId == companyId)
            .ToListAsync();
    }
}

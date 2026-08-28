using WebApp.Data;
using WebApp.Models.Identity;

namespace WebApp.Services.Application;

// Reads company-scoped portal metadata such as company entity, licenses, and permissions.
public interface IApplicationCompanyContextService
{
    Task<ApplicationCompany?> GetCompanyAsync(ApplicationDbContext context, Guid companyId);
    Task<List<ApplicationCompanyLicense>> GetLicensesAsync(ApplicationDbContext context, Guid companyId);
    Task<List<ApplicationCompanyPermission>> GetPermissionsAsync(ApplicationDbContext context, Guid companyId);
}

using WebApp.Data;
using WebApp.Models.Identity;

namespace WebApp.Services.Application;

public interface IApplicationContextService
{
    Task<ApplicationUser?> GetUserByEmailAsync(ApplicationDbContext context, string email);
    Task<ApplicationCompany?> GetCompanyAsync(ApplicationDbContext context, Guid companyId);
    Task<List<ApplicationCompanyConnectionStrings>> GetConnectionStringsAsync(ApplicationDbContext context, Guid companyId);
    Task<List<ApplicationCompanyLicense>> GetLicensesAsync(ApplicationDbContext context, Guid companyId);
    Task<List<ApplicationCompanyPermission>> GetPermissionsAsync(ApplicationDbContext context, Guid companyId);
}

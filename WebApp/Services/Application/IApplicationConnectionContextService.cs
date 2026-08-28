using WebApp.Data;
using WebApp.Models.Identity;

namespace WebApp.Services.Application;

// Reads connection-string mappings for a company from the portal database.
public interface IApplicationConnectionContextService
{
    Task<List<ApplicationCompanyConnectionStrings>> GetConnectionStringsAsync(ApplicationDbContext context, Guid companyId);
}

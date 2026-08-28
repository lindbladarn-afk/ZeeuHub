using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Identity;

namespace WebApp.Services.Application;

public sealed class ApplicationConnectionContextService : IApplicationConnectionContextService
{
    public Task<List<ApplicationCompanyConnectionStrings>> GetConnectionStringsAsync(ApplicationDbContext context, Guid companyId)
    {
        return context.ConnectionStrings!
            .Where(x => x.CompanyId == companyId)
            .ToListAsync();
    }
}

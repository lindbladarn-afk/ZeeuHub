// Resolves company module access against the portal identity database.
using Microsoft.EntityFrameworkCore;
using WebApp.Data;

namespace WebApp.Services.Application;

public interface ICompanyPermissionGuard
{
    Task<bool> HasAccessAsync(Guid companyId, Guid subModuleId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Checks company permissions against the Identity.CompanyPermissions table.
/// </summary>
public class CompanyPermissionGuard : ICompanyPermissionGuard
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public CompanyPermissionGuard(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<bool> HasAccessAsync(Guid companyId, Guid subModuleId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.CompanyPermissions!
            .AnyAsync(cp =>
                cp.CompanyId == companyId
                && (cp.SubModuleId == subModuleId || cp.ModuleId == subModuleId),
                cancellationToken);
    }
}

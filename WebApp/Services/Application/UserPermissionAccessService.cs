using Entities.ViewModels;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;

namespace WebApp.Services.Application;

// Resolves effective user access without allowing grants outside the company's permissions.
public interface IUserPermissionAccessService
{
    Task<bool> HasAccessAsync(Guid companyId, string? userId, Guid permissionId, CancellationToken cancellationToken = default);
    Task<SideMenuViewModel> ApplyToMenuAsync(SideMenuViewModel menu, Guid companyId, string? userId, CancellationToken cancellationToken = default);
}

public sealed class UserPermissionAccessService : IUserPermissionAccessService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public UserPermissionAccessService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<bool> HasAccessAsync(Guid companyId, string? userId, Guid permissionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var usesCustomPermissions = await db.Users
            .Where(user => user.Id == userId && user.CompanyId == companyId)
            .Select(user => (bool?)user.UseCustomPermissions)
            .SingleOrDefaultAsync(cancellationToken);

        if (usesCustomPermissions is null)
            return false;
        if (!usesCustomPermissions.Value)
            return true;

        return await db.UserPermissions.AnyAsync(
            permission => permission.UserId == userId
                && permission.CompanyId == companyId
                && (permission.SubModuleId == permissionId || permission.ModuleId == permissionId),
            cancellationToken);
    }

    public async Task<SideMenuViewModel> ApplyToMenuAsync(
        SideMenuViewModel menu,
        Guid companyId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var result = Clone(menu);
        if (string.IsNullOrWhiteSpace(userId))
        {
            DenyAll(result);
            return result;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var usesCustomPermissions = await db.Users
            .Where(user => user.Id == userId && user.CompanyId == companyId)
            .Select(user => (bool?)user.UseCustomPermissions)
            .SingleOrDefaultAsync(cancellationToken);

        if (usesCustomPermissions is null)
        {
            DenyAll(result);
            return result;
        }
        if (!usesCustomPermissions.Value)
            return result;

        var grants = await db.UserPermissions
            .Where(permission => permission.UserId == userId && permission.CompanyId == companyId)
            .Select(permission => new { permission.ModuleId, permission.SubModuleId })
            .ToListAsync(cancellationToken);

        var moduleIds = grants.Select(grant => grant.ModuleId).ToHashSet();
        var moduleWideIds = grants.Where(grant => !grant.SubModuleId.HasValue)
            .Select(grant => grant.ModuleId)
            .ToHashSet();
        var subModuleIds = grants.Where(grant => grant.SubModuleId.HasValue)
            .Select(grant => grant.SubModuleId!.Value)
            .ToHashSet();

        foreach (var module in result.Modules ?? [])
        {
            module.CompanyHasPermission = module.CompanyHasPermission && moduleIds.Contains(module.Id);
            foreach (var subModule in module.SubModules ?? [])
                subModule.UserHasPermission = subModule.UserHasPermission
                    && (subModuleIds.Contains(subModule.Id) || moduleWideIds.Contains(subModule.ModuleId));
        }

        return result;
    }

    private static void DenyAll(SideMenuViewModel menu)
    {
        foreach (var module in menu.Modules ?? [])
        {
            module.CompanyHasPermission = false;
            foreach (var subModule in module.SubModules ?? [])
                subModule.UserHasPermission = false;
        }
    }

    private static SideMenuViewModel Clone(SideMenuViewModel source)
        => new()
        {
            Modules = (source.Modules ?? []).Select(module => new SideMenuModulesViewModel
            {
                Id = module.Id,
                Name = module.Name,
                Description = module.Description,
                MenuSectionController = module.MenuSectionController,
                MenuSectionAction = module.MenuSectionAction,
                MenuSectionIcon = module.MenuSectionIcon,
                MenuSectionText = module.MenuSectionText,
                MenuSectionEnabled = module.MenuSectionEnabled,
                MenuSectionSortOrder = module.MenuSectionSortOrder,
                CompanyHasPermission = module.CompanyHasPermission,
                SubModules = (module.SubModules ?? []).Select(subModule => new SideMenuSubModuleViewModel
                {
                    Id = subModule.Id,
                    ModuleId = subModule.ModuleId,
                    Name = subModule.Name,
                    Description = subModule.Description,
                    MenuItemController = subModule.MenuItemController,
                    MenuItemAction = subModule.MenuItemAction,
                    MenuItemText = subModule.MenuItemText,
                    MenuItemEnabled = subModule.MenuItemEnabled,
                    MenuItemSortOrder = subModule.MenuItemSortOrder,
                    UserHasPermission = subModule.UserHasPermission
                }).ToList()
            }).ToList()
        };
}

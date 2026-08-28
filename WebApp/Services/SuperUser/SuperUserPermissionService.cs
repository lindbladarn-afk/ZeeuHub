using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Identity;
using WebApp.ViewModels.SuperUser;

namespace WebApp.Services.SuperUser;

// Reads and updates user visibility while enforcing the owning company's permission boundary.
public interface ISuperUserPermissionService
{
    Task<UserPermissionsViewModel?> GetEditorAsync(Guid companyId, string userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Guid companyId, UserPermissionsViewModel model, CancellationToken cancellationToken = default);
}

public sealed class SuperUserPermissionService : ISuperUserPermissionService
{
    private readonly ApplicationDbContext _db;

    public SuperUserPermissionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<UserPermissionsViewModel?> GetEditorAsync(
        Guid companyId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId && candidate.CompanyId == companyId, cancellationToken);
        if (user is null)
            return null;

        var available = await GetAvailablePermissionsAsync(companyId, cancellationToken);
        var selected = await _db.UserPermissions
            .AsNoTracking()
            .Where(permission => permission.UserId == userId && permission.CompanyId == companyId)
            .Select(permission => new PermissionKey(permission.ModuleId, permission.SubModuleId))
            .ToListAsync(cancellationToken);
        var selectedSet = selected.ToHashSet();

        return new UserPermissionsViewModel
        {
            UserId = user.Id,
            UserDisplayName = string.Join(' ', new[] { user.FirstName, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))),
            Email = user.Email ?? string.Empty,
            InheritCompanyPermissions = !user.UseCustomPermissions,
            Groups = available
                .GroupBy(item => new { item.ModuleId, item.ModuleName })
                .OrderBy(group => group.Key.ModuleName)
                .Select(group => new UserPermissionGroupViewModel
                {
                    ModuleId = group.Key.ModuleId,
                    Name = group.Key.ModuleName,
                    Items = group.OrderBy(item => item.Name).Select(item => new UserPermissionItemViewModel
                    {
                        ModuleId = item.ModuleId,
                        SubModuleId = item.SubModuleId,
                        Name = item.Name,
                        Description = item.Description,
                        Selected = selectedSet.Contains(new PermissionKey(item.ModuleId, item.SubModuleId))
                    }).ToList()
                }).ToList()
        };
    }

    public async Task<bool> UpdateAsync(
        Guid companyId,
        UserPermissionsViewModel model,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == model.UserId && candidate.CompanyId == companyId, cancellationToken);
        if (user is null)
            return false;

        var available = (await GetAvailablePermissionsAsync(companyId, cancellationToken))
            .Select(item => new PermissionKey(item.ModuleId, item.SubModuleId))
            .ToHashSet();
        var requested = model.Groups
            .SelectMany(group => group.Items)
            .Where(item => item.Selected)
            .Select(item => new PermissionKey(item.ModuleId, item.SubModuleId))
            .Distinct()
            .ToList();

        if (requested.Any(permission => !available.Contains(permission)))
            return false;

        var existing = await _db.UserPermissions
            .Where(permission => permission.UserId == user.Id)
            .ToListAsync(cancellationToken);
        _db.UserPermissions.RemoveRange(existing);

        user.UseCustomPermissions = !model.InheritCompanyPermissions;
        if (user.UseCustomPermissions)
        {
            _db.UserPermissions.AddRange(requested.Select(permission => new ApplicationUserPermission
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CompanyId = companyId,
                ModuleId = permission.ModuleId,
                SubModuleId = permission.SubModuleId
            }));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<List<AvailablePermission>> GetAvailablePermissionsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var companyPermissions = await _db.CompanyPermissions!
            .AsNoTracking()
            .Where(permission => permission.CompanyId == companyId)
            .Select(permission => new { ModuleId = permission.ModuleId!.Value, permission.SubModuleId })
            .ToListAsync(cancellationToken);
        var moduleIds = companyPermissions.Select(permission => permission.ModuleId).Distinct().ToList();
        var modules = await _db.Modules!.AsNoTracking()
            .Where(module => moduleIds.Contains(module.Id))
            .ToDictionaryAsync(module => module.Id, cancellationToken);
        var subModuleIds = companyPermissions.Where(permission => permission.SubModuleId.HasValue)
            .Select(permission => permission.SubModuleId!.Value)
            .Distinct()
            .ToList();
        var subModules = await _db.SubModules!.AsNoTracking()
            .Where(subModule => subModuleIds.Contains(subModule.Id))
            .ToDictionaryAsync(subModule => subModule.Id, cancellationToken);

        return companyPermissions
            .Where(permission => modules.ContainsKey(permission.ModuleId))
            .Select(permission =>
            {
                subModules.TryGetValue(permission.SubModuleId ?? Guid.Empty, out var subModule);
                var module = modules[permission.ModuleId];
                return new AvailablePermission(
                    permission.ModuleId,
                    permission.SubModuleId,
                    module.Name ?? "Modul",
                    subModule?.Name ?? module.Name ?? "Modul",
                    subModule?.Description ?? module.Description);
            })
            .DistinctBy(permission => new PermissionKey(permission.ModuleId, permission.SubModuleId))
            .ToList();
    }

    private sealed record AvailablePermission(Guid ModuleId, Guid? SubModuleId, string ModuleName, string Name, string? Description);
    private sealed record PermissionKey(Guid ModuleId, Guid? SubModuleId);
}

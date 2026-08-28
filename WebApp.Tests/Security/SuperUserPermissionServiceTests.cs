using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Identity;
using WebApp.Services.SuperUser;
using WebApp.ViewModels.SuperUser;

namespace WebApp.Tests;

// Verifies the editor stays inside the owning company and persists custom grants.
public sealed class SuperUserPermissionServiceTests
{
    [Fact]
    public async Task UpdateAsync_RejectsUserFromAnotherCompany()
    {
        await using var db = CreateDb();
        var user = CreateUser(Guid.NewGuid());
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new SuperUserPermissionService(db);
        var updated = await service.UpdateAsync(Guid.NewGuid(), new UserPermissionsViewModel { UserId = user.Id });

        Assert.False(updated);
    }

    [Fact]
    public async Task UpdateAsync_RejectsPermissionTheCompanyDoesNotOwn()
    {
        await using var db = CreateDb();
        var companyId = Guid.NewGuid();
        var user = CreateUser(companyId);
        db.Users.Add(user);
        await SeedOwnedPermissionAsync(db, companyId);
        await db.SaveChangesAsync();

        var service = new SuperUserPermissionService(db);
        var model = new UserPermissionsViewModel
        {
            UserId = user.Id,
            Groups =
            [
                new UserPermissionGroupViewModel
                {
                    Items = [new UserPermissionItemViewModel { ModuleId = Guid.NewGuid(), Selected = true }]
                }
            ]
        };

        var updated = await service.UpdateAsync(companyId, model);

        Assert.False(updated);
        Assert.Empty(db.UserPermissions);
    }

    [Fact]
    public async Task UpdateAsync_SavesSelectedPermissions_AndTogglesInheritance()
    {
        await using var db = CreateDb();
        var companyId = Guid.NewGuid();
        var user = CreateUser(companyId);
        var moduleId = Guid.NewGuid();
        var grantedSubModuleId = Guid.NewGuid();

        db.Users.Add(user);
        db.Modules!.Add(new ApplicationModule
        {
            Id = moduleId,
            Name = "Module",
            Description = "Module description"
        });
        db.SubModules!.Add(new ApplicationSubModule
        {
            Id = grantedSubModuleId,
            ModuleId = moduleId,
            Name = "Sub module",
            Description = "Sub module description"
        });
        db.CompanyPermissions!.Add(new ApplicationCompanyPermission
        {
            CompanyId = companyId,
            ModuleId = moduleId,
            SubModuleId = grantedSubModuleId
        });
        await db.SaveChangesAsync();

        var service = new SuperUserPermissionService(db);
        var model = new UserPermissionsViewModel
        {
            UserId = user.Id,
            InheritCompanyPermissions = false,
            Groups =
            [
                new UserPermissionGroupViewModel
                {
                    ModuleId = moduleId,
                    Items =
                    [
                        new UserPermissionItemViewModel
                        {
                            ModuleId = moduleId,
                            SubModuleId = grantedSubModuleId,
                            Selected = true
                        }
                    ]
                }
            ]
        };

        var updated = await service.UpdateAsync(companyId, model);

        Assert.True(updated);
        Assert.True(user.UseCustomPermissions);
        Assert.Single(db.UserPermissions);
        Assert.Equal(grantedSubModuleId, db.UserPermissions.Single().SubModuleId);
    }

    private static ApplicationDbContext CreateDb()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static ApplicationUser CreateUser(Guid companyId)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            FirstName = "Test",
            LastName = "User",
            CompanyId = companyId
        };

    private static Task SeedOwnedPermissionAsync(ApplicationDbContext db, Guid companyId)
    {
        var moduleId = Guid.NewGuid();
        db.Modules!.Add(new ApplicationModule
        {
            Id = moduleId,
            Name = "Owned module"
        });
        db.CompanyPermissions!.Add(new ApplicationCompanyPermission
        {
            CompanyId = companyId,
            ModuleId = moduleId
        });
        return Task.CompletedTask;
    }
}

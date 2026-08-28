using Entities.ViewModels;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Identity;
using WebApp.Services.Application;

namespace WebApp.Tests;

// Verifies the user-level permission overlay used by the sidebar and permission editor.
public sealed class UserPermissionAccessServiceTests
{
    [Fact]
    public async Task HasAccessAsync_InheritsCompanyAccess_WhenCustomPermissionsAreDisabled()
    {
        var fixture = CreateFixture(useCustomPermissions: false);
        await using (fixture.Db)
        {
            var allowed = await fixture.Service.HasAccessAsync(fixture.CompanyId, fixture.User.Id, Guid.NewGuid());
            Assert.True(allowed);
        }
    }

    [Fact]
    public async Task HasAccessAsync_DeniesWhenUserIdIsMissing()
    {
        var fixture = CreateFixture(useCustomPermissions: false);
        await using (fixture.Db)
        {
            var allowed = await fixture.Service.HasAccessAsync(fixture.CompanyId, null, Guid.NewGuid());
            Assert.False(allowed);
        }
    }

    [Fact]
    public async Task HasAccessAsync_AllowsOnlySelectedPermission_WhenCustomPermissionsAreEnabled()
    {
        var fixture = CreateFixture(useCustomPermissions: true);
        var allowedId = Guid.NewGuid();
        fixture.Db.UserPermissions.Add(new ApplicationUserPermission
        {
            Id = Guid.NewGuid(),
            UserId = fixture.User.Id,
            CompanyId = fixture.CompanyId,
            ModuleId = Guid.NewGuid(),
            SubModuleId = allowedId
        });
        await fixture.Db.SaveChangesAsync();

        await using (fixture.Db)
        {
            Assert.True(await fixture.Service.HasAccessAsync(fixture.CompanyId, fixture.User.Id, allowedId));
            Assert.False(await fixture.Service.HasAccessAsync(fixture.CompanyId, fixture.User.Id, Guid.NewGuid()));
        }
    }

    [Fact]
    public async Task ApplyToMenuAsync_RemovesUnselectedItemsWithoutMutatingSource()
    {
        var fixture = CreateFixture(useCustomPermissions: true);
        var moduleId = Guid.NewGuid();
        var allowedId = Guid.NewGuid();
        var deniedId = Guid.NewGuid();

        fixture.Db.UserPermissions.Add(new ApplicationUserPermission
        {
            Id = Guid.NewGuid(),
            UserId = fixture.User.Id,
            CompanyId = fixture.CompanyId,
            ModuleId = moduleId,
            SubModuleId = allowedId
        });
        await fixture.Db.SaveChangesAsync();

        var source = new SideMenuViewModel
        {
            Modules =
            [
                new SideMenuModulesViewModel
                {
                    Id = moduleId,
                    CompanyHasPermission = true,
                    SubModules =
                    [
                        new SideMenuSubModuleViewModel { Id = allowedId, ModuleId = moduleId, UserHasPermission = true },
                        new SideMenuSubModuleViewModel { Id = deniedId, ModuleId = moduleId, UserHasPermission = true }
                    ]
                }
            ]
        };

        await using (fixture.Db)
        {
            var result = await fixture.Service.ApplyToMenuAsync(source, fixture.CompanyId, fixture.User.Id);

            Assert.True(result.Modules[0].SubModules![0].UserHasPermission);
            Assert.False(result.Modules[0].SubModules![1].UserHasPermission);
            Assert.True(source.Modules[0].SubModules![1].UserHasPermission);
        }
    }

    [Fact]
    public async Task ApplyToMenuAsync_DeniesAllWhenUserIdIsMissing()
    {
        var fixture = CreateFixture(useCustomPermissions: true);
        var moduleId = Guid.NewGuid();
        var source = new SideMenuViewModel
        {
            Modules =
            [
                new SideMenuModulesViewModel
                {
                    Id = moduleId,
                    CompanyHasPermission = true,
                    SubModules =
                    [
                        new SideMenuSubModuleViewModel { Id = Guid.NewGuid(), ModuleId = moduleId, UserHasPermission = true }
                    ]
                }
            ]
        };

        await using (fixture.Db)
        {
            var result = await fixture.Service.ApplyToMenuAsync(source, fixture.CompanyId, null);

            Assert.False(result.Modules[0].CompanyHasPermission);
            Assert.False(result.Modules[0].SubModules![0].UserHasPermission);
        }
    }

    private static Fixture CreateFixture(bool useCustomPermissions)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ApplicationDbContext(options);
        var companyId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("N"),
            FirstName = "Test",
            LastName = "User",
            CompanyId = companyId,
            UseCustomPermissions = useCustomPermissions
        };

        db.Users.Add(user);
        db.SaveChanges();

        return new Fixture(
            new UserPermissionAccessService(new TestDbContextFactory(options)),
            db,
            companyId,
            user);
    }

    private sealed record Fixture(UserPermissionAccessService Service, ApplicationDbContext Db, Guid CompanyId, ApplicationUser User);

    private sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        {
            _options = options;
        }

        public ApplicationDbContext CreateDbContext() => new(_options);

        public ValueTask<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(CreateDbContext());
    }
}

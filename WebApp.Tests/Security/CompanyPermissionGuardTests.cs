using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Identity;
using WebApp.Services.Application;

namespace WebApp.Tests;

// Verifies the permission guard accepts both module and legacy submodule grants.
public sealed class CompanyPermissionGuardTests
{
    [Fact]
    public async Task HasAccessAsync_ReturnsTrue_For_ModulePermission()
    {
        var companyId = Guid.NewGuid();
        var moduleId = Guid.NewGuid();
        var options = CreateOptions();

        await using (var db = new ApplicationDbContext(options))
        {
            db.CompanyPermissions!.Add(new ApplicationCompanyPermission
            {
                CompanyId = companyId,
                ModuleId = moduleId
            });
            await db.SaveChangesAsync();
        }

        var guard = new CompanyPermissionGuard(new TestDbContextFactory(options));
        var hasAccess = await guard.HasAccessAsync(companyId, moduleId);

        Assert.True(hasAccess);
    }

    [Fact]
    public async Task HasAccessAsync_ReturnsTrue_For_SubModulePermission()
    {
        var companyId = Guid.NewGuid();
        var moduleId = Guid.NewGuid();
        var subModuleId = Guid.NewGuid();
        var options = CreateOptions();

        await using (var db = new ApplicationDbContext(options))
        {
            db.CompanyPermissions!.Add(new ApplicationCompanyPermission
            {
                CompanyId = companyId,
                ModuleId = moduleId,
                SubModuleId = subModuleId
            });
            await db.SaveChangesAsync();
        }

        var guard = new CompanyPermissionGuard(new TestDbContextFactory(options));
        var hasAccess = await guard.HasAccessAsync(companyId, subModuleId);

        Assert.True(hasAccess);
    }

    [Fact]
    public async Task HasAccessAsync_ReturnsFalse_When_NoPermissionExists()
    {
        var options = CreateOptions();
        var guard = new CompanyPermissionGuard(new TestDbContextFactory(options));

        var hasAccess = await guard.HasAccessAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(hasAccess);
    }

    private static DbContextOptions<ApplicationDbContext> CreateOptions()
        => new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

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

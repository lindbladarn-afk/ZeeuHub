using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Identity;
using WebApp.Seeding;

namespace WebApp.Tests;

// Seeder tests cover safe initialization decisions and targeted menu normalization.
public sealed class MenuAutoSeederTests
{
    [Theory]
    [InlineData(false, 0, true)]
    [InlineData(false, 1, false)]
    [InlineData(true, 0, false)]
    [InlineData(true, 1, false)]
    public void ShouldCreateTables_OnlyRepairsAnExistingDatabaseWithoutApplicationTables(
        bool databaseWasCreated,
        long applicationTableCount,
        bool expected)
    {
        var result = PortalDatabaseInitializer.ShouldCreateTables(databaseWasCreated, applicationTableCount);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task InitializeAsync_SupportsNonRelationalTestProviders()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new ApplicationDbContext(options);

        await PortalDatabaseInitializer.InitializeAsync(db);

        Assert.True(await db.Database.CanConnectAsync());
    }

    [Fact]
    public async Task HideCustomerSyncModuleAsync_Disables_Any_CustomerSync_Module_Row()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new ApplicationDbContext(options);
        var module = new ApplicationModule
        {
            Id = Guid.NewGuid(),
            Name = "CustomerSync",
            Description = "Top-level customer sync menu entry",
            MenuSectionController = "Integration",
            MenuSectionAction = "CustomerSync",
            MenuSectionText = "Module_CustomerSync",
            MenuSectionEnabled = true,
            MenuSectionSortOrder = 999
        };

        db.Modules!.Add(module);
        await db.SaveChangesAsync();

        await MenuAutoSeeder.HideCustomerSyncModuleAsync(db);
        await db.SaveChangesAsync();

        var stored = await db.Modules!.SingleAsync(item => item.Id == module.Id);
        Assert.False(stored.MenuSectionEnabled);
    }
}

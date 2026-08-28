// Verifies persistence mappings that protect portal data across schema changes.
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Telemetry;
using WebApp.Models.Integration.BankReconciliation;

namespace WebApp.Tests;

public sealed class ApplicationDbContextModelTests
{
    [Fact]
    public async Task UserUsageTotals_AllowsOneRowPerUserAndCompany()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var context = new ApplicationDbContext(options);
        var userId = "user-1";

        context.UserUsageTotals!.AddRange(
            new UserUsageTotal { UserId = userId, CompanyId = Guid.NewGuid(), TotalMinutes = 10 },
            new UserUsageTotal { UserId = userId, CompanyId = Guid.NewGuid(), TotalMinutes = 20 });

        await context.SaveChangesAsync();

        var keyProperties = context.Model
            .FindEntityType(typeof(UserUsageTotal))!
            .FindPrimaryKey()!
            .Properties
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal(new[] { nameof(UserUsageTotal.UserId), nameof(UserUsageTotal.CompanyId) }, keyProperties);
        Assert.Equal(2, await context.UserUsageTotals.CountAsync());
    }

    [Fact]
    public void BankReconciliationRecords_UseCompositeKeysAndConcurrencyVersions()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        using var context = new ApplicationDbContext(options);

        var stateType = context.Model.FindEntityType(typeof(BankReconciliationStateRecord))!;
        var keyNames = stateType.FindPrimaryKey()!.Properties.Select(property => property.Name);

        Assert.Equal(
            new[]
            {
                nameof(BankReconciliationStateRecord.CompanyId),
                nameof(BankReconciliationStateRecord.StateKeyHash)
            },
            keyNames);
        Assert.True(stateType.FindProperty(nameof(BankReconciliationStateRecord.Version))!.IsConcurrencyToken);
        Assert.True(context.Model
            .FindEntityType(typeof(BankReconciliationImportRegistryRecord))!
            .FindProperty(nameof(BankReconciliationImportRegistryRecord.Version))!
            .IsConcurrencyToken);
        Assert.True(context.Model
            .FindEntityType(typeof(BankReconciliationCodingRuleRecord))!
            .FindProperty(nameof(BankReconciliationCodingRuleRecord.Version))!
            .IsConcurrencyToken);
    }
}

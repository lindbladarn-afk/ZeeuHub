using Entities.Application;
using WebApp.Models.Integration;
using WebApp.Services.Integration.BankReconciliation.CodingRules;

namespace WebApp.Tests;

// Coding rule tests verify that per-company bank account matrices round-trip safely.
public sealed class BankReconciliationCodingRuleServiceTests
{
    private readonly TestApplicationDbContextFactory _contextFactory = new();

    public BankReconciliationCodingRuleServiceTests()
    {
    }

    [Fact]
    public async Task SaveAsync_RoundsTripRowsPerBankAccount()
    {
        var service = CreateService();
        var rows = new[]
        {
            new BankReconciliationCodingRuleRow
            {
                TypeKey = "def",
                TypeLabel = "DEF",
                RuleLabel = "Standard",
                Account = "1910",
                CostCenter = "100"
            },
            new BankReconciliationCodingRuleRow
            {
                TypeKey = "bankinbetalningar",
                TypeLabel = "Bankinbetalningar",
                RuleLabel = "PMNT/RCDT",
                Account = "1510",
                CostCenter = "200"
            }
        };

        var saved = await service.SaveAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "SE0395000099602664071202",
            new UserSession { UserId = "user-1", Email = "user@example.com" },
            rows,
            "WILLAB GARDEN AB · SE0395000099602664071202");

        var loaded = await service.LoadAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "se0395000099602664071202");

        Assert.Equal(1, saved.Version);
        Assert.Equal(1, loaded.Version);
        Assert.Equal("11111111-1111-1111-1111-111111111111", loaded.CompanyId);
        Assert.Equal("SE0395000099602664071202", loaded.BankAccountKey);
        Assert.Equal("1510", loaded.Rows.First(x => x.TypeKey == "bankinbetalningar").Account);
        Assert.Equal("100", loaded.Rows.First(x => x.TypeKey == "def").CostCenter);
    }

    [Fact]
    public async Task SaveAsync_ThrowsConflict_OnVersionMismatch()
    {
        var service = CreateService();
        var companyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        await service.SaveAsync(
            companyId,
            "SE0395000099602664071202",
            new UserSession { UserId = "user-1", Email = "user@example.com" },
            new[]
            {
                new BankReconciliationCodingRuleRow
                {
                    TypeKey = "def",
                    TypeLabel = "DEF",
                    RuleLabel = "Standard",
                    Account = "1910"
                }
            },
            "WILLAB GARDEN AB · SE0395000099602664071202");

        await Assert.ThrowsAsync<BankReconciliationCodingRuleConflictException>(() => service.SaveAsync(
            companyId,
            "SE0395000099602664071202",
            new UserSession { UserId = "user-1", Email = "user@example.com" },
            new[]
            {
                new BankReconciliationCodingRuleRow
                {
                    TypeKey = "def",
                    TypeLabel = "DEF",
                    RuleLabel = "Standard",
                    Account = "1930"
                }
            },
            "WILLAB GARDEN AB · SE0395000099602664071202",
            expectedVersion: 0));
    }

    [Fact]
    public async Task LoadAsync_UsesSpecificRowsBeforeCompanyDefaults()
    {
        var service = CreateService();
        var companyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        await service.SaveAsync(
            companyId,
            "default",
            new UserSession { UserId = "user-default", Email = "default@example.com" },
            new[]
            {
                new BankReconciliationCodingRuleRow
                {
                    TypeKey = "def",
                    TypeLabel = "DEF",
                    RuleLabel = "Standard",
                    Account = "1910",
                    CostCenter = "100"
                },
                new BankReconciliationCodingRuleRow
                {
                    TypeKey = "bankinbetalningar",
                    TypeLabel = "Bankinbetalningar",
                    RuleLabel = "PMNT/RCDT",
                    Account = "1510",
                    CostCenter = "200"
                },
                new BankReconciliationCodingRuleRow
                {
                    TypeKey = "rantekonto",
                    TypeLabel = "Räntekonto",
                    RuleLabel = "Ränta",
                    Account = "8310",
                    CostCenter = "300"
                }
            },
            "Bolagsstandard");

        await service.SaveAsync(
            companyId,
            "SE0395000099602664071202",
            new UserSession { UserId = "user-specific", Email = "specific@example.com" },
            new[]
            {
                new BankReconciliationCodingRuleRow
                {
                    TypeKey = "def",
                    TypeLabel = "DEF",
                    RuleLabel = "Standard",
                    Account = "1930",
                    CostCenter = "400"
                },
                new BankReconciliationCodingRuleRow
                {
                    TypeKey = "bankinbetalningar",
                    TypeLabel = "Bankinbetalningar",
                    RuleLabel = "PMNT/RCDT",
                    Account = "1520",
                    CostCenter = "210"
                }
            },
            "WILLAB GARDEN AB · SE0395000099602664071202");

        var loaded = await service.LoadAsync(
            companyId,
            "SE0395000099602664071202");

        var defRow = loaded.Rows.Single(x => x.TypeKey == "def");
        var receiptRow = loaded.Rows.Single(x => x.TypeKey == "bankinbetalningar");
        var interestRow = loaded.Rows.Single(x => x.TypeKey == "rantekonto");

        Assert.Equal("1930", defRow.Account);
        Assert.Equal("SE0395000099602664071202", defRow.SourceBankAccountKey);
        Assert.False(defRow.IsInherited);

        Assert.Equal("1520", receiptRow.Account);
        Assert.Equal("SE0395000099602664071202", receiptRow.SourceBankAccountKey);
        Assert.False(receiptRow.IsInherited);

        Assert.Equal("8310", interestRow.Account);
        Assert.Equal("DEFAULT", interestRow.SourceBankAccountKey);
        Assert.True(interestRow.IsInherited);
    }

    private BankReconciliationCodingRuleService CreateService()
        => new(_contextFactory);
}

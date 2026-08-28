using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Models.Integration;
using WebApp.Services.Integration.BankReconciliation.CodingRules;
using WebApp.Services.Integration.BankReconciliation.Commands;

namespace WebApp.Tests;

// Tests the command boundary that validates and saves bank reconciliation coding rules.
public sealed class BankReconciliationCodingRuleCommandServiceTests
{
    [Fact]
    public async Task SaveAsync_ReturnsFailure_WhenCompanyIsMissing()
    {
        var service = CreateService(new CapturingCodingRuleService());

        var result = await service.SaveAsync(
            new UserSession { CompanyId = Guid.Empty },
            new BankReconciliationCodingRuleSaveRequest { BankAccountKey = "SEB-123" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Saknar aktivt bolag.", result.ErrorMessage);
    }

    [Fact]
    public async Task SaveAsync_ReturnsFailure_WhenBankAccountIsMissing()
    {
        var service = CreateService(new CapturingCodingRuleService());

        var result = await service.SaveAsync(
            new UserSession { CompanyId = Guid.NewGuid() },
            new BankReconciliationCodingRuleSaveRequest { BankAccountKey = " " },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Saknar bankkonto för reglerna.", result.ErrorMessage);
    }

    [Fact]
    public async Task SaveAsync_PersistsRules_AndMapsResult()
    {
        var codingRuleService = new CapturingCodingRuleService
        {
            SaveResult = new BankReconciliationCodingRuleSet
            {
                Version = 4,
                BankAccountKey = "SEB-123",
                BankAccountLabel = "SEB Företagskonto",
                Rows =
                [
                    new BankReconciliationCodingRuleRow
                    {
                        RowId = "row-1",
                        TypeKey = "bankavgift",
                        RuleLabel = "Bankavgift",
                        Account = "6570"
                    }
                ]
            }
        };
        var service = CreateService(codingRuleService);
        var companyId = Guid.NewGuid();
        var user = new UserSession { CompanyId = companyId, UserId = "user-1" };
        var request = new BankReconciliationCodingRuleSaveRequest
        {
            BankAccountKey = "SEB-123",
            BankAccountLabel = "SEB Företagskonto",
            ExpectedVersion = 3,
            Rows =
            [
                new BankReconciliationCodingRuleRow
                {
                    RowId = "row-1",
                    TypeKey = "bankavgift",
                    RuleLabel = "Bankavgift",
                    Account = "6570"
                }
            ]
        };

        var result = await service.SaveAsync(user, request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(4, result.Version);
        Assert.Equal("SEB-123", result.BankAccountKey);
        Assert.Equal("SEB Företagskonto", result.BankAccountLabel);
        Assert.Single(result.Rows);
        Assert.Equal(companyId, codingRuleService.CapturedCompanyId);
        Assert.Equal("SEB-123", codingRuleService.CapturedBankAccountKey);
        Assert.Same(user, codingRuleService.CapturedUser);
        Assert.Equal(3, codingRuleService.CapturedExpectedVersion);
        Assert.Equal("SEB Företagskonto", codingRuleService.CapturedBankAccountLabel);
        Assert.Single(codingRuleService.CapturedRows);
    }

    [Fact]
    public async Task SaveAsync_ReturnsConflict_WhenRulesChanged()
    {
        var service = CreateService(
            new CapturingCodingRuleService { ConflictVersion = 7 });

        var result = await service.SaveAsync(
            new UserSession { CompanyId = Guid.NewGuid() },
            new BankReconciliationCodingRuleSaveRequest { BankAccountKey = "SEB-123" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.Conflict);
        Assert.Equal(7, result.CurrentVersion);
        Assert.Contains("Referens:", result.ErrorMessage);
        Assert.DoesNotContain("secret-value", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static BankReconciliationCodingRuleCommandService CreateService(CapturingCodingRuleService codingRuleService)
        => new(
            codingRuleService,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            NullLogger<BankReconciliationCodingRuleCommandService>.Instance);

    private sealed class CapturingCodingRuleService : IBankReconciliationCodingRuleService
    {
        public Guid CapturedCompanyId { get; private set; }
        public string? CapturedBankAccountKey { get; private set; }
        public UserSession? CapturedUser { get; private set; }
        public IReadOnlyList<BankReconciliationCodingRuleRow> CapturedRows { get; private set; } = [];
        public string? CapturedBankAccountLabel { get; private set; }
        public int? CapturedExpectedVersion { get; private set; }
        public int? ConflictVersion { get; init; }
        public BankReconciliationCodingRuleSet SaveResult { get; init; } = new();

        public Task<BankReconciliationCodingRuleSet> LoadAsync(
            Guid companyId,
            string bankAccountKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationCodingRuleSet());

        public Task<BankReconciliationCodingRuleSet> SaveAsync(
            Guid companyId,
            string bankAccountKey,
            UserSession? user,
            IReadOnlyList<BankReconciliationCodingRuleRow> rows,
            string? bankAccountLabel = null,
            int? expectedVersion = null,
            CancellationToken cancellationToken = default)
        {
            if (ConflictVersion is int currentVersion)
            {
                throw new BankReconciliationCodingRuleConflictException(currentVersion);
            }

            CapturedCompanyId = companyId;
            CapturedBankAccountKey = bankAccountKey;
            CapturedUser = user;
            CapturedRows = rows;
            CapturedBankAccountLabel = bankAccountLabel;
            CapturedExpectedVersion = expectedVersion;

            return Task.FromResult(SaveResult);
        }
    }
}

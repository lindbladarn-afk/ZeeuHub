using Microsoft.EntityFrameworkCore;
using WebApp.Models.Integration;
using WebApp.Services.Integration.BankReconciliation.Imports;

namespace WebApp.Tests;

// Import registry tests protect idempotency boundaries between companies, accounts and statements.
public sealed class BankReconciliationImportRegistryTests
{
    private static readonly Guid CompanyA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly TestApplicationDbContextFactory _contextFactory = new();
    private readonly BankReconciliationImportRegistry _registry;

    public BankReconciliationImportRegistryTests()
    {
        _registry = new BankReconciliationImportRegistry(_contextFactory);
    }

    [Fact]
    public async Task RegisterAsync_IdenticalDocument_ReturnsExactDuplicate()
    {
        var document = Document("STMT-1", "SE-ACCOUNT-A", "TX-A", "TX-B");

        var first = await RegisterAsync(CompanyA, document);
        var second = await RegisterAsync(CompanyA, document);

        Assert.Equal(BankReconciliationImportStatus.New, first.Status);
        Assert.Equal(BankReconciliationImportStatus.ExactDuplicate, second.Status);
        Assert.False(second.Accepted);
    }

    [Fact]
    public async Task RegisterAsync_DifferentStatementWithSharedTransaction_ReturnsOverlap()
    {
        await RegisterAsync(CompanyA, Document("STMT-1", "SE-ACCOUNT-A", "TX-A", "TX-B"));

        var result = await RegisterAsync(CompanyA, Document("STMT-2", "SE-ACCOUNT-A", "TX-B", "TX-C"));

        Assert.Equal(BankReconciliationImportStatus.Overlapping, result.Status);
        Assert.Equal(1, result.OverlappingTransactionCount);
        Assert.False(result.Accepted);
    }

    [Fact]
    public async Task RegisterAsync_ChangedVersionOfSameStatement_ReturnsCorrected()
    {
        await RegisterAsync(CompanyA, Document("STMT-1", "SE-ACCOUNT-A", "TX-A", "TX-B"));

        var result = await RegisterAsync(CompanyA, Document("STMT-1", "SE-ACCOUNT-A", "TX-A", "TX-C"));

        Assert.Equal(BankReconciliationImportStatus.Corrected, result.Status);
        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task RegisterAsync_SameTransactionsForOtherCompanyOrAccount_RemainIsolated()
    {
        var source = Document("STMT-1", "SE-ACCOUNT-A", "TX-A");
        await RegisterAsync(CompanyA, source);

        var otherCompany = await RegisterAsync(CompanyB, source);
        var otherAccount = await RegisterAsync(CompanyA, Document("STMT-1", "SE-ACCOUNT-B", "TX-A"));

        Assert.Equal(BankReconciliationImportStatus.New, otherCompany.Status);
        Assert.Equal(BankReconciliationImportStatus.New, otherAccount.Status);
    }

    [Fact]
    public async Task RegisterAsync_PersistedRegistry_DoesNotContainRawBankIdentifiers()
    {
        await RegisterAsync(CompanyA, Document("SENSITIVE-STMT", "SENSITIVE-ACCOUNT", "TX-A"));

        await using var context = await _contextFactory.CreateDbContextAsync();
        var json = (await context.BankReconciliationImportRegistries.SingleAsync()).RegistryJson;

        Assert.DoesNotContain("SENSITIVE-STMT", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SENSITIVE-ACCOUNT", json, StringComparison.Ordinal);
    }

    private Task<BankReconciliationImportRegistrationResult> RegisterAsync(
        Guid companyId,
        BankReconciliationParsedDocument document)
        => _registry.RegisterAsync(new BankReconciliationImportRegistrationRequest
        {
            CompanyId = companyId,
            Document = document
        });

    private static BankReconciliationParsedDocument Document(
        string statementId,
        string account,
        params string[] transactionFingerprints)
    {
        var entry = new BankReconciliationParsedEntry
        {
            Transactions = transactionFingerprints.Select(fingerprint => new BankReconciliationParsedTransaction
            {
                DuplicateFingerprint = fingerprint
            }).ToList()
        };
        return new BankReconciliationParsedDocument
        {
            Statements =
            {
                new BankReconciliationParsedStatement
                {
                    StatementId = statementId,
                    AccountIban = account,
                    AccountCurrency = "SEK",
                    Entries = { entry }
                }
            }
        };
    }
}

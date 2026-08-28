using Entities.Application;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;
using WebApp.Services.Integration.BankReconciliation;
using WebApp.Services.Integration.BankReconciliation.CodingRules;
using WebApp.Services.Integration.BankReconciliation.Workspace;
using WebApp.ViewModels.Shared;

namespace WebApp.Tests;

// Workspace tests keep demo scenario data isolated from uploaded CAMT sources.
public sealed class BankReconciliationWorkspaceServiceTests
{
    [Fact]
    public async Task ResolveSourceAsync_PartialPayments_UsesScenarioTransactionsInsteadOfUploadedAiFile()
    {
        var parser = new TrackingParser();
        var service = CreateService(parser);

        var result = await service.ResolveSourceAsync(
            User(),
            "/tmp/previous-ai-file.xml",
            isDemoMode: true,
            demoScenarioKey: "partial-payments");

        Assert.True(result.HasSource);
        Assert.Equal("partial-payments", result.DemoScenarioKey);
        Assert.Contains(result.Transactions, transaction => transaction.Id == "TX-P1011-A");
        Assert.Equal(0, parser.CallCount);
    }

    [Fact]
    public async Task ResolveSourceAsync_AiCamtLab_UsesUploadedCamtWhenAvailable()
    {
        var parser = new TrackingParser
        {
            Transactions =
            {
                new BankReconciliationParsedTransaction { Id = "TX-FROM-CAMT", Amount = 100m }
            }
        };
        var service = CreateService(parser);

        var result = await service.ResolveSourceAsync(
            User(),
            "/tmp/ai-camt.xml",
            isDemoMode: true,
            demoScenarioKey: "ai-camt-lab");

        Assert.True(result.HasSource);
        Assert.Contains(result.Transactions, transaction => transaction.Id == "TX-FROM-CAMT");
        Assert.Equal(1, parser.CallCount);
    }

    [Fact]
    public async Task ResolveSourceAsync_LegacyTransactionMatch_MigratesToStableIdentifier()
    {
        var parser = new TrackingParser
        {
            Transactions =
            {
                new BankReconciliationParsedTransaction
                {
                    Id = "TX-STABLE123",
                    LegacyId = "TX-001",
                    Amount = 100m
                }
            }
        };
        var bankService = new FakeBankReconciliationService
        {
            State = new BankReconciliationPersistedState
            {
                Version = 4,
                Matches =
                {
                    new BankReconciliationSavedMatch
                    {
                        TransactionId = "TX-001",
                        InvoiceId = "INV-1",
                        MatchedAmount = 100m
                    }
                }
            }
        };
        var service = CreateService(parser, bankService);

        var result = await service.ResolveSourceAsync(
            User(),
            "/tmp/legacy-camt.xml",
            isDemoMode: true,
            demoScenarioKey: "ai-camt-lab");

        var transaction = Assert.Single(result.Transactions);
        Assert.Equal("INV-1", transaction.MatchedInvoiceId);
        Assert.Equal("TX-STABLE123", Assert.Single(bankService.State.Matches).TransactionId);
        Assert.Equal("migrate-transaction-identities", bankService.LastAuditAction);
    }

    [Fact]
    public async Task ResolveSourceAsync_ParseFailure_SanitizesErrorMessage()
    {
        var service = CreateService(new ThrowingParser());

        var result = await service.ResolveSourceAsync(
            User(),
            "/tmp/bad-camt.xml",
            isDemoMode: false,
            demoScenarioKey: "ai-camt-lab");

        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.DoesNotContain("authorization=secret-value", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static BankReconciliationWorkspaceService CreateService(
        IBankReconciliationCamtParser parser,
        FakeBankReconciliationService? bankService = null)
    {
        var webAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));
        return new BankReconciliationWorkspaceService(
            parser,
            new FakeCodingRuleService(),
            new BankReconciliationDemoDataService(new TestHostEnvironment
            {
                ContentRootPath = webAppRoot,
                ContentRootFileProvider = new PhysicalFileProvider(webAppRoot)
            }),
            bankService ?? new FakeBankReconciliationService(),
            new DummyStringLocalizer());
    }

    private static UserSession User()
        => new() { CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111") };

    private sealed class TrackingParser : IBankReconciliationCamtParser
    {
        public int CallCount { get; private set; }
        public List<BankReconciliationParsedTransaction> Transactions { get; } = new();

        public IReadOnlyList<BankReconciliationParsedTransaction> Parse(string filePath)
        {
            CallCount += 1;
            return Transactions;
        }
    }

    private sealed class ThrowingParser : IBankReconciliationCamtParser
    {
        public IReadOnlyList<BankReconciliationParsedTransaction> Parse(string filePath)
            => throw new InvalidOperationException("authorization=secret-value");
    }

    private sealed class FakeCodingRuleService : IBankReconciliationCodingRuleService
    {
        public Task<BankReconciliationCodingRuleSet> LoadAsync(Guid companyId, string bankAccountKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationCodingRuleSet());

        public Task<BankReconciliationCodingRuleSet> SaveAsync(
            Guid companyId,
            string bankAccountKey,
            UserSession? user,
            IReadOnlyList<BankReconciliationCodingRuleRow> rows,
            string? bankAccountLabel = null,
            int? expectedVersion = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationCodingRuleSet());
    }

    private sealed class FakeBankReconciliationService : IBankReconciliationService
    {
        public BankReconciliationPersistedState State { get; set; } = new();
        public string? LastAuditAction { get; private set; }

        public IReadOnlyList<BankReconciliationRecommendationItem> BuildRecommendations(
            BankReconciliationTransactionCandidate transaction,
            IReadOnlyList<InvoiceItem> invoices,
            IReadOnlyDictionary<string, decimal> allocatedAmountsByInvoiceId,
            int maxResults = 4) => Array.Empty<BankReconciliationRecommendationItem>();

        public BankReconciliationAutoMatchResult BuildAutoMatches(
            IReadOnlyList<BankReconciliationTransactionCandidate> transactions,
            IReadOnlyList<InvoiceItem> invoices) => new();

        public Task<BankReconciliationAiSuggestionResult> BuildAiSuggestionsAsync(
            BankReconciliationAiSuggestionRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(new BankReconciliationAiSuggestionResult());

        public Task<BankReconciliationPersistedState> LoadStateAsync(
            Guid companyId,
            string stateKey,
            CancellationToken cancellationToken = default) => Task.FromResult(State);

        public Task<BankReconciliationPersistedState> ReplaceMatchesAsync(
            Guid companyId,
            string stateKey,
            UserSession? user,
            IReadOnlyList<BankReconciliationSavedMatch> matches,
            string auditActionType,
            int? expectedVersion = null,
            string? note = null,
            CancellationToken cancellationToken = default)
        {
            LastAuditAction = auditActionType;
            State = new BankReconciliationPersistedState
            {
                Version = State.Version + 1,
                Matches = matches.ToList()
            };
            return Task.FromResult(State);
        }

        public Task<BankReconciliationPersistedState> UpsertMatchAsync(
            Guid companyId,
            string stateKey,
            UserSession? user,
            BankReconciliationSavedMatch match,
            int? expectedVersion = null,
            string? note = null,
            CancellationToken cancellationToken = default) => Task.FromResult(new BankReconciliationPersistedState());

        public Task<BankReconciliationPersistedState> ReverseMatchAsync(
            Guid companyId,
            string stateKey,
            UserSession? user,
            string transactionId,
            string? allocationId = null,
            string? invoiceId = null,
            int? expectedVersion = null,
            string? reason = null,
            CancellationToken cancellationToken = default) => Task.FromResult(new BankReconciliationPersistedState());
    }

    private sealed class DummyStringLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }
}

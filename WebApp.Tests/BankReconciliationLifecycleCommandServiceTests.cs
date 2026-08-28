// Verifies that completion is validated server-side before a reconciliation is locked.
using Entities.Application;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;
using WebApp.Services.Integration.BankReconciliation;
using WebApp.Services.Integration.BankReconciliation.Commands;
using WebApp.Services.Integration.BankReconciliation.Invoices;
using WebApp.Services.Integration.BankReconciliation.Presentation;
using WebApp.Services.Integration.BankReconciliation.Workspace;

namespace WebApp.Tests;

public sealed class BankReconciliationLifecycleCommandServiceTests
{
    private static readonly Guid CompanyId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task CloseAsync_RejectsUnmatchedTransactions()
    {
        var stateService = new BankReconciliationStateService(
            new TestApplicationDbContextFactory());
        var service = CreateService(stateService);
        var source = Source(CreateTransaction());

        var result = await service.CloseAsync(
            source,
            User(),
            expectedVersion: 0,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, result.UnmatchedCount);
        Assert.False((await stateService.LoadAsync(CompanyId, source.StateKey!)).IsClosed);
    }

    [Fact]
    public async Task CloseAndReopen_PersistLifecycleAndReason()
    {
        var stateService = new BankReconciliationStateService(
            new TestApplicationDbContextFactory());
        var openState = await stateService.ReplaceMatchesAsync(
            CompanyId,
            "statement-1",
            User(),
            new[]
            {
                new BankReconciliationSavedMatch
                {
                    TransactionId = "TX-1",
                    InvoiceId = "INV-1",
                    MatchedAmount = 100m
                }
            },
            "replace-matches");
        var transaction = CreateTransaction();
        transaction.Allocations.Add(new BankReconciliationParsedAllocation
        {
            AllocationId = "allocation-1",
            InvoiceId = "INV-1",
            MatchedAmount = 100m
        });
        var source = Source(transaction);
        var service = CreateService(stateService);

        var closed = await service.CloseAsync(
            source,
            User(),
            openState.Version,
            CancellationToken.None);
        var invalidReopen = await service.ReopenAsync(
            source,
            User(),
            closed.Version,
            "x",
            CancellationToken.None);
        var reopened = await service.ReopenAsync(
            source,
            User(),
            closed.Version,
            "Korrigerad faktura.",
            CancellationToken.None);

        Assert.True(closed.Success);
        Assert.True(closed.IsClosed);
        Assert.False(invalidReopen.Success);
        Assert.True(reopened.Success);
        Assert.False(reopened.IsClosed);
        var persisted = await stateService.LoadAsync(CompanyId, "statement-1");
        Assert.Equal("Korrigerad faktura.", persisted.AuditTrail.Last().Note);
    }

    private static BankReconciliationLifecycleCommandService CreateService(
        IBankReconciliationStateService stateService)
        => new(
            stateService,
            new FakeInvoiceCandidateService(),
            new BankReconciliationTransactionPageService(
                new NoRecommendationBankReconciliationService()),
            new FakeWorkspaceService());

    private static BankReconciliationSourceContext Source(
        BankReconciliationParsedTransaction transaction)
        => new()
        {
            HasSource = true,
            StateKey = "statement-1",
            BankAccountKey = "ACCOUNT-1",
            Transactions = { transaction }
        };

    private static BankReconciliationParsedTransaction CreateTransaction()
        => new()
        {
            Id = "TX-1",
            Amount = 100m,
            Currency = "SEK",
            Group = "Kundinbetalningar",
            Classification = new BankReconciliationTransactionClassification
            {
                TypeKey = "bankinbetalningar",
                TypeLabel = "Bankinbetalningar",
                LegacyGroup = "Kundinbetalningar"
            }
        };

    private static UserSession User()
        => new()
        {
            CompanyId = CompanyId,
            UserId = "user-1",
            FirstName = "Test",
            LastName = "User"
        };

    private sealed class FakeInvoiceCandidateService
        : IBankReconciliationInvoiceCandidateService
    {
        public Task<BankReconciliationInvoiceCandidateResult> LoadAsync(
            bool isDemoMode,
            UserSession user,
            CancellationToken cancellationToken,
            BankReconciliationParsedTransaction? transaction = null,
            string? classificationFilter = null,
            string? groupFilter = null,
            int? page = null,
            int? pageSize = null,
            string? demoScenarioKey = null)
            => Task.FromResult(new BankReconciliationInvoiceCandidateResult());

        public Task<BankReconciliationInvoiceCandidateResult> LoadCustomerPageAsync(
            UserSession user,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
            => Task.FromResult(new BankReconciliationInvoiceCandidateResult());

        public Task<BankReconciliationInvoiceCandidateResult> LoadSupplierPageAsync(
            UserSession user,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
            => Task.FromResult(new BankReconciliationInvoiceCandidateResult());
    }

    private sealed class FakeWorkspaceService : IBankReconciliationWorkspaceService
    {
        public Task<BankReconciliationSourceContext> ResolveSourceAsync(
            UserSession? user,
            string? sessionFile,
            bool isDemoMode,
            string demoScenarioKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationSourceContext());

        public Task<BankReconciliationCodingRuleSet> LoadCodingRulesAsync(
            UserSession? user,
            BankReconciliationSourceContext source,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationCodingRuleSet { Version = 3 });

        public Task ResetDemoScenarioAsync(
            Guid companyId,
            string scenarioKey,
            UserSession? user,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoRecommendationBankReconciliationService
        : IBankReconciliationService
    {
        public IReadOnlyList<BankReconciliationRecommendationItem> BuildRecommendations(
            BankReconciliationTransactionCandidate transaction,
            IReadOnlyList<InvoiceItem> invoices,
            IReadOnlyDictionary<string, decimal> allocatedAmountsByInvoiceId,
            int maxResults = 4)
            => Array.Empty<BankReconciliationRecommendationItem>();

        public BankReconciliationAutoMatchResult BuildAutoMatches(
            IReadOnlyList<BankReconciliationTransactionCandidate> transactions,
            IReadOnlyList<InvoiceItem> invoices)
            => new();

        public Task<BankReconciliationAiSuggestionResult> BuildAiSuggestionsAsync(
            BankReconciliationAiSuggestionRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationAiSuggestionResult());

        public Task<BankReconciliationPersistedState> LoadStateAsync(
            Guid companyId,
            string stateKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState());

        public Task<BankReconciliationPersistedState> ReplaceMatchesAsync(
            Guid companyId,
            string stateKey,
            UserSession? user,
            IReadOnlyList<BankReconciliationSavedMatch> matches,
            string auditActionType,
            int? expectedVersion = null,
            string? note = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState());

        public Task<BankReconciliationPersistedState> UpsertMatchAsync(
            Guid companyId,
            string stateKey,
            UserSession? user,
            BankReconciliationSavedMatch match,
            int? expectedVersion = null,
            string? note = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState());

        public Task<BankReconciliationPersistedState> ReverseMatchAsync(
            Guid companyId,
            string stateKey,
            UserSession? user,
            string transactionId,
            string? allocationId = null,
            string? invoiceId = null,
            int? expectedVersion = null,
            string? reason = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState());
    }
}

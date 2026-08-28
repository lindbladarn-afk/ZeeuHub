using Entities.Application;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;
using WebApp.Services.Integration.BankReconciliation;
using WebApp.Services.Integration.BankReconciliation.Queries;

namespace WebApp.Tests;

// State query tests cover persisted-state projection without MVC.
public sealed class BankReconciliationStateQueryServiceTests
{
    [Fact]
    public async Task BuildState_ReturnsMatchesAndLatestEightActivities()
    {
        var bankService = new FakeBankReconciliationService
        {
            State = new BankReconciliationPersistedState
            {
                Version = 7,
                Matches =
                {
                    new BankReconciliationSavedMatch
                    {
                        AllocationId = "alloc-1",
                        TransactionId = "TX-1",
                        InvoiceId = "INV-1",
                        MatchType = "manual",
                        MatchRule = "manual",
                        MatchedAmount = 125m,
                        Currency = "SEK"
                    }
                },
                AuditTrail = Enumerable.Range(1, 10)
                    .Select(index => new BankReconciliationAuditEntry
                    {
                        CreatedAtUtc = new DateTime(2026, 6, index, 8, 0, 0, DateTimeKind.Utc),
                        ActionType = $"action-{index}",
                        UserName = "Test User"
                    })
                    .ToList()
            }
        };
        var service = new BankReconciliationStateQueryService(bankService);

        var result = await service.BuildStateAsync(
            new BankReconciliationSourceContext { StateKey = "state-1" },
            new UserSession { CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111") },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(7, result.Version);
        Assert.Equal(1, result.MatchCount);
        Assert.Single(result.Matches);
        Assert.Equal("alloc-1", result.Matches[0].AllocationId);
        Assert.Equal(8, result.RecentActivity.Count);
        Assert.Equal("action-10", result.RecentActivity[0].ActionType);
        Assert.Equal("action-3", result.RecentActivity[^1].ActionType);
    }

    private sealed class FakeBankReconciliationService : IBankReconciliationService
    {
        public BankReconciliationPersistedState State { get; set; } = new();

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
            => Task.FromResult(State);

        public Task<BankReconciliationPersistedState> ReplaceMatchesAsync(
            Guid companyId,
            string stateKey,
            UserSession? user,
            IReadOnlyList<BankReconciliationSavedMatch> matches,
            string auditActionType,
            int? expectedVersion = null,
            string? note = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(State);

        public Task<BankReconciliationPersistedState> UpsertMatchAsync(
            Guid companyId,
            string stateKey,
            UserSession? user,
            BankReconciliationSavedMatch match,
            int? expectedVersion = null,
            string? note = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(State);

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
            => Task.FromResult(State);
    }
}

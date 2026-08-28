using Entities.Application;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;
using WebApp.Services.Integration.BankReconciliation;

namespace WebApp.Tests;

// Service tests verify that coding-aware invoice matching keeps the recommendation flow aligned.
public sealed class BankReconciliationServiceTests
{
    [Fact]
    public async Task BuildAiSuggestionsAsync_SkipsNonInvoiceMatchingCoding()
    {
        var service = new BankReconciliationService(
            new FakeAiSuggestionService(),
            new BankReconciliationMatchingService(),
            new FakeStateService());

        var result = await service.BuildAiSuggestionsAsync(new BankReconciliationAiSuggestionRequest
        {
            CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            StateKey = "state-1",
            Transaction = new BankReconciliationTransactionCandidate
            {
                TransactionId = "TX-1",
                Amount = 100m,
                Currency = "SEK",
                ResolvedCodingTypeKey = "overforing-konto"
            },
            RuleCandidates = new List<BankReconciliationRecommendationItem>
            {
                new()
            }
        });

        Assert.Equal("skipped-coding-rule", result.Status);
        Assert.Empty(result.Suggestions);
    }

    private sealed class FakeAiSuggestionService : IBankReconciliationAiSuggestionService
    {
        public Task<BankReconciliationAiSuggestionResult> BuildSuggestionsAsync(BankReconciliationAiSuggestionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationAiSuggestionResult
            {
                Enabled = true,
                Status = "enabled",
                Suggestions = new List<BankReconciliationAiSuggestionCandidate>
                {
                    new()
                    {
                        InvoiceId = "1001",
                        MatchedAmount = 100m
                    }
                }
            });
    }

    private sealed class FakeStateService : IBankReconciliationStateService
    {
        public Task<BankReconciliationPersistedState> LoadAsync(Guid companyId, string stateKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState());

        public Task<BankReconciliationPersistedState> ReplaceMatchesAsync(Guid companyId, string stateKey, UserSession? user, IReadOnlyList<BankReconciliationSavedMatch> matches, string auditActionType, int? expectedVersion = null, string? note = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState());

        public Task<BankReconciliationPersistedState> UpsertMatchAsync(Guid companyId, string stateKey, UserSession? user, BankReconciliationSavedMatch match, int? expectedVersion = null, string? note = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState());

        public Task<BankReconciliationPersistedState> ReverseMatchAsync(Guid companyId, string stateKey, UserSession? user, string transactionId, string? allocationId = null, string? invoiceId = null, int? expectedVersion = null, string? reason = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState());

        public Task<BankReconciliationPersistedState> CloseAsync(Guid companyId, string stateKey, UserSession? user, int? expectedVersion, string sourceFingerprint, int codingRulesVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState { IsClosed = true });

        public Task<BankReconciliationPersistedState> ReopenAsync(Guid companyId, string stateKey, UserSession? user, int? expectedVersion, string reason, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState());
    }
}

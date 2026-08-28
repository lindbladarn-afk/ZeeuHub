using Entities.Application;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;
using WebApp.Services.Integration.BankReconciliation;
using WebApp.Services.Integration.BankReconciliation.Presentation;

namespace WebApp.Tests;

// Transaction page tests cover presentation totals without routing through the controller.
public sealed class BankReconciliationTransactionPageServiceTests
{
    [Fact]
    public void BuildPage_CalculatesUnmatchedAcrossCustomerAndSupplierPayments()
    {
        var service = new BankReconciliationTransactionPageService(new FakeBankReconciliationService());
        var transactions = new List<BankReconciliationParsedTransaction>
        {
            CreateTransaction("TX-1", 100m, "Kundinbetalningar", "bankinbetalningar", new BankReconciliationParsedAllocation
            {
                AllocationId = "a1",
                InvoiceId = "1001",
                MatchedAmount = 100m
            }),
            CreateTransaction("TX-2", -250m, "Leverantorsutbetalningar", "leverantorsbetalning"),
            CreateTransaction("TX-3", -25m, "Ovrigt", "bankavgift")
        };

        var result = service.BuildPage(
            transactions,
            Array.Empty<InvoiceItem>(),
            page: 1,
            pageSize: 20,
            filter: "all",
            groupFilter: "all",
            classificationFilter: "all");

        Assert.Equal(100m, result.Totals.Credit);
        Assert.Equal(-275m, result.Totals.Debit);
        Assert.Equal(100m, result.Totals.Matched);
        Assert.Equal(250m, result.Totals.Unmatched);
    }

    [Fact]
    public void BuildPage_TreatsPartialAllocationAsReviewInsteadOfComplete()
    {
        var service = new BankReconciliationTransactionPageService(new FakeBankReconciliationService());
        var transactions = new[]
        {
            CreateTransaction(
                "TX-PARTIAL",
                100m,
                "Kundinbetalningar",
                "bankinbetalningar",
                new BankReconciliationParsedAllocation
                {
                    AllocationId = "allocation-1",
                    InvoiceId = "INV-1",
                    MatchedAmount = 40m
                })
        };

        var result = service.BuildPage(
            transactions,
            Array.Empty<InvoiceItem>(),
            page: 1,
            pageSize: 20,
            filter: "all",
            groupFilter: "all",
            classificationFilter: "all");

        Assert.Equal(0, result.Summary.Matched);
        Assert.Equal(1, result.Summary.Review);
        Assert.Equal(0, result.Summary.Unmatched);
    }

    [Fact]
    public void BuildPage_StatusFiltersMatchTheCompletionSummary()
    {
        var service = new BankReconciliationTransactionPageService(
            new FakeBankReconciliationService("TX-REVIEW"));
        var transactions = new[]
        {
            CreateTransaction(
                "TX-MATCHED",
                100m,
                "Kundinbetalningar",
                "bankinbetalningar",
                new BankReconciliationParsedAllocation
                {
                    AllocationId = "allocation-matched",
                    InvoiceId = "INV-MATCHED",
                    MatchedAmount = 100m
                }),
            CreateTransaction(
                "TX-PARTIAL",
                100m,
                "Kundinbetalningar",
                "bankinbetalningar",
                new BankReconciliationParsedAllocation
                {
                    AllocationId = "allocation-partial",
                    InvoiceId = "INV-PARTIAL",
                    MatchedAmount = 40m
                }),
            CreateTransaction("TX-REVIEW", 100m, "Kundinbetalningar", "bankinbetalningar"),
            CreateTransaction("TX-UNMATCHED", 100m, "Kundinbetalningar", "bankinbetalningar")
        };

        var matched = BuildPage(service, transactions, "matched");
        var review = BuildPage(service, transactions, "review");
        var unmatched = BuildPage(service, transactions, "unmatched");

        Assert.Equal(["TX-MATCHED"], matched.Items.Select(item => item.Id));
        Assert.Equal(["TX-PARTIAL", "TX-REVIEW"], review.Items.Select(item => item.Id).Order());
        Assert.Equal(["TX-UNMATCHED"], unmatched.Items.Select(item => item.Id));

        Assert.Equal(1, matched.Summary.Matched);
        Assert.Equal(2, matched.Summary.Review);
        Assert.Equal(1, matched.Summary.Unmatched);
        Assert.Equal(matched.Summary.Matched, matched.TotalCount);
        Assert.Equal(review.Summary.Review, review.TotalCount);
        Assert.Equal(unmatched.Summary.Unmatched, unmatched.TotalCount);
    }

    private static BankReconciliationTransactionPageResult BuildPage(
        BankReconciliationTransactionPageService service,
        IReadOnlyList<BankReconciliationParsedTransaction> transactions,
        string filter)
        => service.BuildPage(
            transactions,
            Array.Empty<InvoiceItem>(),
            page: 1,
            pageSize: 20,
            filter: filter,
            groupFilter: "all",
            classificationFilter: "all");

    private static BankReconciliationParsedTransaction CreateTransaction(
        string id,
        decimal amount,
        string group,
        string typeKey,
        params BankReconciliationParsedAllocation[] allocations)
    {
        return new BankReconciliationParsedTransaction
        {
            Id = id,
            Date = "2026-05-17",
            ValueDate = "2026-05-17",
            Amount = amount,
            Currency = "SEK",
            Group = group,
            Classification = new BankReconciliationTransactionClassification
            {
                TypeKey = typeKey,
                TypeLabel = typeKey,
                RuleLabel = "test",
                LegacyGroup = group
            },
            Allocations = allocations.ToList()
        };
    }

    private sealed class FakeBankReconciliationService : IBankReconciliationService
    {
        private readonly HashSet<string> _recommendedTransactionIds;

        public FakeBankReconciliationService(params string[] recommendedTransactionIds)
        {
            _recommendedTransactionIds = recommendedTransactionIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<BankReconciliationRecommendationItem> BuildRecommendations(
            BankReconciliationTransactionCandidate transaction,
            IReadOnlyList<InvoiceItem> invoices,
            IReadOnlyDictionary<string, decimal> allocatedAmountsByInvoiceId,
            int maxResults = 4)
            => _recommendedTransactionIds.Contains(transaction.TransactionId)
                ?
                [
                    new BankReconciliationRecommendationItem
                    {
                        Invoice = new BankReconciliationRecommendationInvoice
                        {
                            Id = "INV-REVIEW",
                            InvoiceNo = "INV-REVIEW",
                            Amount = transaction.Amount,
                            RemainingAmount = transaction.Amount,
                            Currency = transaction.Currency
                        },
                        RequiresManualConfirmation = true
                    }
                ]
                : Array.Empty<BankReconciliationRecommendationItem>();

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

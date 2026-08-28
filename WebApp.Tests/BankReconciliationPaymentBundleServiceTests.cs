using Entities.Application;
using Microsoft.AspNetCore.Http;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;
using WebApp.Services.Integration.BankReconciliation;
using WebApp.Services.Integration.BankReconciliation.Bundles;
using WebApp.Services.Integration.BankReconciliation.Invoices;

namespace WebApp.Tests;

// Payment bundle workflow tests verify stale proposals are rejected and accepted bundles use one state write.
public sealed class BankReconciliationPaymentBundleServiceTests
{
    [Fact]
    public async Task ConfirmAsync_CurrentSuggestion_AppendsAllAllocationsInSingleWrite()
    {
        var bankService = new FakeBankReconciliationService();
        var matcher = new FakeBundleMatcher(CreateSuggestion());
        var service = new BankReconciliationPaymentBundleService(
            bankService,
            new FakeInvoiceCandidateService(),
            matcher,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationPaymentBundleService>.Instance);

        var result = await service.ConfirmAsync(
            Source(),
            User(),
            new BankReconciliationConfirmPaymentBundleRequest
            {
                BundleId = "bundle-1",
                ExpectedVersion = 3
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Matches.Count);
        Assert.Equal(2, bankService.ReplacedMatches.Count);
        Assert.Equal(1, bankService.ReplaceCallCount);
        Assert.All(result.Matches, match => Assert.Equal("payment-bundle", match.MatchRule));
    }

    [Fact]
    public async Task ConfirmAsync_StaleVersion_DoesNotWrite()
    {
        var bankService = new FakeBankReconciliationService();
        var service = new BankReconciliationPaymentBundleService(
            bankService,
            new FakeInvoiceCandidateService(),
            new FakeBundleMatcher(CreateSuggestion()),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationPaymentBundleService>.Instance);

        var result = await service.ConfirmAsync(
            Source(),
            User(),
            new BankReconciliationConfirmPaymentBundleRequest
            {
                BundleId = "bundle-1",
                ExpectedVersion = 2
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.Conflict);
        Assert.Equal(3, result.CurrentVersion);
        Assert.Equal(0, bankService.ReplaceCallCount);
    }

    [Fact]
    public async Task ConfirmAsync_MissingVersion_DoesNotWrite()
    {
        var bankService = new FakeBankReconciliationService();
        var service = new BankReconciliationPaymentBundleService(
            bankService,
            new FakeInvoiceCandidateService(),
            new FakeBundleMatcher(CreateSuggestion()),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationPaymentBundleService>.Instance);

        var result = await service.ConfirmAsync(
            Source(),
            User(),
            new BankReconciliationConfirmPaymentBundleRequest { BundleId = "bundle-1" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("state-version", result.ErrorMessage);
        Assert.Equal(0, bankService.ReplaceCallCount);
    }

    [Fact]
    public async Task ConfirmManualAsync_ValidSelection_AppendsAllocationsInSingleWrite()
    {
        var bankService = new FakeBankReconciliationService();
        var service = CreateService(bankService);

        var result = await service.ConfirmManualAsync(
            Source(),
            User(),
            new BankReconciliationConfirmManualPaymentBundleRequest
            {
                InvoiceId = "INV-1",
                TransactionIds = new List<string> { "TX-1", "TX-2" },
                ExpectedVersion = 3
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Matches.Count);
        Assert.Equal(2, bankService.ReplacedMatches.Count);
        Assert.Equal(1, bankService.ReplaceCallCount);
        Assert.All(result.Matches, match =>
        {
            Assert.Equal("manual", match.MatchType);
            Assert.Equal("manual-payment-bundle", match.MatchRule);
            Assert.Equal("INV-1", match.InvoiceId);
        });
    }

    [Fact]
    public async Task ConfirmManualAsync_Overpayment_DoesNotWrite()
    {
        var bankService = new FakeBankReconciliationService();
        var service = CreateService(bankService);
        var source = Source();
        source.Transactions[1].Amount = 70m;

        var result = await service.ConfirmManualAsync(
            source,
            User(),
            new BankReconciliationConfirmManualPaymentBundleRequest
            {
                InvoiceId = "INV-1",
                TransactionIds = new List<string> { "TX-1", "TX-2" },
                ExpectedVersion = 3
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("överstiger", result.ErrorMessage);
        Assert.Equal(0, bankService.ReplaceCallCount);
    }

    [Fact]
    public async Task ConfirmManualAsync_StaleVersion_DoesNotWrite()
    {
        var bankService = new FakeBankReconciliationService();
        var service = CreateService(bankService);

        var result = await service.ConfirmManualAsync(
            Source(),
            User(),
            new BankReconciliationConfirmManualPaymentBundleRequest
            {
                InvoiceId = "INV-1",
                TransactionIds = new List<string> { "TX-1", "TX-2" },
                ExpectedVersion = 2
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.Conflict);
        Assert.Equal(3, result.CurrentVersion);
        Assert.Equal(0, bankService.ReplaceCallCount);
    }

    private static BankReconciliationPaymentBundleService CreateService(
        FakeBankReconciliationService bankService)
        => new(
            bankService,
            new FakeInvoiceCandidateService(),
            new FakeBundleMatcher(CreateSuggestion()),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationPaymentBundleService>.Instance);

    private static BankReconciliationPaymentBundleSuggestion CreateSuggestion()
        => new()
        {
            BundleId = "bundle-1",
            InvoiceId = "INV-1",
            InvoiceNo = "INV-1",
            Allocations =
            {
                new BankReconciliationPaymentBundleAllocation { TransactionId = "TX-1", MatchedAmount = 40m },
                new BankReconciliationPaymentBundleAllocation { TransactionId = "TX-2", MatchedAmount = 60m }
            }
        };

    private static BankReconciliationSourceContext Source()
        => new()
        {
            StateKey = "state-1",
            Transactions =
            {
                new BankReconciliationParsedTransaction
                {
                    Id = "TX-1",
                    Date = "2026-01-10",
                    EntryStatus = "BOOK",
                    Direction = "CRDT",
                    Amount = 40m,
                    Currency = "SEK",
                    Classification = new BankReconciliationTransactionClassification
                    {
                        TypeKey = "bankinbetalningar",
                        IsDefault = false
                    }
                },
                new BankReconciliationParsedTransaction
                {
                    Id = "TX-2",
                    Date = "2026-01-11",
                    EntryStatus = "BOOK",
                    Direction = "CRDT",
                    Amount = 60m,
                    Currency = "SEK",
                    Classification = new BankReconciliationTransactionClassification
                    {
                        TypeKey = "bankinbetalningar",
                        IsDefault = false
                    }
                }
            }
        };

    private static UserSession User()
        => new()
        {
            UserId = "user-1",
            FirstName = "Ada",
            LastName = "Lovelace",
            CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111")
        };

    private sealed class FakeBundleMatcher : IBankReconciliationPaymentBundleMatcher
    {
        private readonly BankReconciliationPaymentBundleSuggestion _suggestion;

        public FakeBundleMatcher(BankReconciliationPaymentBundleSuggestion suggestion)
        {
            _suggestion = suggestion;
        }

        public IReadOnlyList<BankReconciliationPaymentBundleSuggestion> BuildSuggestions(
            IReadOnlyList<BankReconciliationTransactionCandidate> transactions,
            IReadOnlyList<InvoiceItem> invoices,
            IReadOnlyList<BankReconciliationSavedMatch> existingMatches)
            => new[] { _suggestion };
    }

    private sealed class FakeInvoiceCandidateService : IBankReconciliationInvoiceCandidateService
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
            => Task.FromResult(new BankReconciliationInvoiceCandidateResult
            {
                Invoices = new List<InvoiceItem>
                {
                    new()
                    {
                        InvoiceNo = "INV-1",
                        RemainingAmount = 100m,
                        Currency = "SEK",
                        IsSupplierInvoice = false
                    }
                }
            });

        public Task<BankReconciliationInvoiceCandidateResult> LoadCustomerPageAsync(
            UserSession user,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
            => LoadAsync(false, user, cancellationToken);

        public Task<BankReconciliationInvoiceCandidateResult> LoadSupplierPageAsync(
            UserSession user,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
            => LoadAsync(false, user, cancellationToken);
    }

    private sealed class FakeBankReconciliationService : IBankReconciliationService
    {
        public int ReplaceCallCount { get; private set; }
        public IReadOnlyList<BankReconciliationSavedMatch> ReplacedMatches { get; private set; } = Array.Empty<BankReconciliationSavedMatch>();

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
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState { Version = 3 });

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
            ReplaceCallCount += 1;
            ReplacedMatches = matches;
            return Task.FromResult(new BankReconciliationPersistedState { Version = 4, Matches = matches.ToList() });
        }

        public Task<BankReconciliationPersistedState> UpsertMatchAsync(
            Guid companyId,
            string stateKey,
            UserSession? user,
            BankReconciliationSavedMatch match,
            int? expectedVersion = null,
            string? note = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<BankReconciliationPersistedState> ReverseMatchAsync(
            Guid companyId,
            string stateKey,
            UserSession? user,
            string transactionId,
            string? allocationId = null,
            string? invoiceId = null,
            int? expectedVersion = null,
            string? reason = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

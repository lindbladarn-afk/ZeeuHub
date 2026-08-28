using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;
using WebApp.Services.Integration.BankReconciliation;
using WebApp.Services.Integration.BankReconciliation.Commands;
using WebApp.Services.Integration.BankReconciliation.Invoices;
using WebApp.ViewModels.Shared;

namespace WebApp.Tests;

// Match command tests cover validation and state writes outside the MVC controller.
public sealed class BankReconciliationMatchCommandServiceTests
{
    [Fact]
    public async Task SaveManualMatch_UsesRemainingTransactionAmount()
    {
        var bankService = new FakeBankReconciliationService
        {
            State = new BankReconciliationPersistedState
            {
                Matches =
                {
                    new BankReconciliationSavedMatch
                    {
                        TransactionId = "TX-1",
                        InvoiceId = "INV-OTHER",
                        MatchedAmount = 40m
                    }
                }
            }
        };
        var invoiceService = new FakeInvoiceCandidateService(new[]
        {
            new InvoiceItem
            {
                InvoiceNo = "INV-1",
                AmountSek = 80m,
                RemainingAmount = 80m,
                DueDate = new DateTime(2026, 5, 31)
            }
        });
        var service = CreateService(bankService, invoiceService);

        var result = await service.SaveManualMatchAsync(
            new BankReconciliationSourceContext
            {
                StateKey = "state-1",
                Transactions =
                {
                    new BankReconciliationParsedTransaction
                    {
                        Id = "TX-1",
                        EntryStatus = "BOOK",
                        Direction = "CRDT",
                        Date = "2026-05-12",
                        Amount = 100m,
                        Currency = "SEK"
                    }
                }
            },
            new UserSession
            {
                UserId = "user-1",
                FirstName = "Ada",
                LastName = "Lovelace",
                CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111")
            },
            new BankReconciliationManualMatchRequest
            {
                TransactionId = "TX-1",
                InvoiceId = "INV-1"
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(60m, bankService.SavedMatch?.MatchedAmount);
        Assert.Equal("Ada Lovelace", bankService.SavedMatch?.CreatedByName);
    }

    [Fact]
    public async Task AutoMatch_PreservesExistingMatchesAndReturnsBundleSuggestions()
    {
        var existing = new BankReconciliationSavedMatch
        {
            AllocationId = "existing-1",
            TransactionId = "TX-EXISTING",
            InvoiceId = "INV-EXISTING",
            MatchType = "manual",
            MatchedAmount = 40m
        };
        var added = new BankReconciliationSavedMatch
        {
            AllocationId = "auto-1",
            TransactionId = "TX-NEW",
            InvoiceId = "INV-NEW",
            MatchType = "auto",
            MatchedAmount = 60m
        };
        var bankService = new FakeBankReconciliationService
        {
            State = new BankReconciliationPersistedState { Version = 7, Matches = { existing } },
            AutoMatches = new BankReconciliationAutoMatchResult { Matches = { added } }
        };
        var bundleMatcher = new FakePaymentBundleMatcher
        {
            Suggestions =
            {
                new BankReconciliationPaymentBundleSuggestion
                {
                    BundleId = "bundle-1",
                    InvoiceId = "INV-BUNDLE",
                    Allocations =
                    {
                        new BankReconciliationPaymentBundleAllocation
                        {
                            TransactionId = "TX-BUNDLE",
                            MatchedAmount = 25m
                        }
                    }
                }
            }
        };
        var service = CreateService(
            bankService,
            new FakeInvoiceCandidateService(new[] { Invoice("INV-NEW", 60m) }),
            bundleMatcher);

        var result = await service.AutoMatchAsync(
            Source(Transaction("TX-EXISTING", 100m), Transaction("TX-NEW", 60m), Transaction("TX-BUNDLE", 25m)),
            User(),
            expectedVersion: 7,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.Count);
        Assert.Equal(2, result.Matches.Count);
        Assert.Contains(result.Matches, match => match.AllocationId == "existing-1");
        Assert.Contains(result.Matches, match => match.AllocationId == "auto-1");
        Assert.Equal(2, bankService.ReplacedMatches.Count);
        Assert.Equal(2, bundleMatcher.LastExistingMatches.Count);
        Assert.DoesNotContain(bankService.LastAutoTransactions, transaction => transaction.TransactionId == "TX-BUNDLE");
        Assert.Single(result.PaymentBundleSuggestions);
    }

    [Fact]
    public async Task SaveManualMatch_ReturnsSupportId_WhenStateConflicts()
    {
        var bankService = new FakeBankReconciliationService
        {
            ConflictVersion = 9
        };
        var service = CreateService(
            bankService,
            new FakeInvoiceCandidateService(new[] { Invoice("INV-1", 100m) }));

        var result = await service.SaveManualMatchAsync(
            Source(new BankReconciliationParsedTransaction
            {
                Id = "TX-1",
                EntryStatus = "BOOK",
                Direction = "CRDT",
                Date = "2026-05-12",
                Amount = 100m,
                Currency = "SEK"
            }),
            User(),
            new BankReconciliationManualMatchRequest
            {
                TransactionId = "TX-1",
                InvoiceId = "INV-1"
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.Conflict);
        Assert.Equal(9, result.CurrentVersion);
        Assert.Contains("Referens:", result.ErrorMessage);
        Assert.DoesNotContain("secret-value", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AutoMatch_UsesRemainingBalancesAndSkipsStateWriteWhenNothingWasAdded()
    {
        var bankService = new FakeBankReconciliationService
        {
            State = new BankReconciliationPersistedState
            {
                Version = 3,
                Matches =
                {
                    new BankReconciliationSavedMatch
                    {
                        TransactionId = "TX-1",
                        InvoiceId = "INV-OTHER",
                        MatchedAmount = 40m
                    }
                }
            }
        };
        var service = CreateService(
            bankService,
            new FakeInvoiceCandidateService(new[] { Invoice("INV-1", 80m) }));

        var result = await service.AutoMatchAsync(
            Source(Transaction("TX-1", 100m)),
            User(),
            expectedVersion: 3,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(60m, Assert.Single(bankService.LastAutoTransactions).Amount);
        Assert.Equal(80m, Assert.Single(bankService.LastAutoInvoices).RemainingAmount);
        Assert.Equal(0, bankService.ReplaceCallCount);
        Assert.Equal(3, result.Version);
        Assert.Single(result.Matches);
    }

    private static BankReconciliationMatchCommandService CreateService(
        FakeBankReconciliationService bankService,
        FakeInvoiceCandidateService invoiceService,
        IBankReconciliationPaymentBundleMatcher? bundleMatcher = null)
        => new(
            bankService,
            invoiceService,
            bundleMatcher ?? new FakePaymentBundleMatcher(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new DummyStringLocalizer(),
            NullLogger<BankReconciliationMatchCommandService>.Instance);

    private static BankReconciliationSourceContext Source(params BankReconciliationParsedTransaction[] transactions)
        => new() { StateKey = "state-1", Transactions = transactions.ToList() };

    private static BankReconciliationParsedTransaction Transaction(string id, decimal amount)
        => new() { Id = id, Amount = amount, Currency = "SEK" };

    private static InvoiceItem Invoice(string invoiceNo, decimal remainingAmount)
        => new()
        {
            InvoiceNo = invoiceNo,
            Ocr = invoiceNo,
            AmountSek = remainingAmount,
            RemainingAmount = remainingAmount,
            DueDate = new DateTime(2026, 6, 30)
        };

    private static UserSession User()
        => new()
        {
            UserId = "user-1",
            FirstName = "Ada",
            LastName = "Lovelace",
            CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111")
        };

    private sealed class FakeBankReconciliationService : IBankReconciliationService
    {
        public BankReconciliationPersistedState State { get; set; } = new();
        public BankReconciliationSavedMatch? SavedMatch { get; private set; }
        public BankReconciliationAutoMatchResult AutoMatches { get; set; } = new();
        public IReadOnlyList<BankReconciliationTransactionCandidate> LastAutoTransactions { get; private set; } = Array.Empty<BankReconciliationTransactionCandidate>();
        public IReadOnlyList<InvoiceItem> LastAutoInvoices { get; private set; } = Array.Empty<InvoiceItem>();
        public IReadOnlyList<BankReconciliationSavedMatch> ReplacedMatches { get; private set; } = Array.Empty<BankReconciliationSavedMatch>();
        public int ReplaceCallCount { get; private set; }

        public IReadOnlyList<BankReconciliationRecommendationItem> BuildRecommendations(
            BankReconciliationTransactionCandidate transaction,
            IReadOnlyList<InvoiceItem> invoices,
            IReadOnlyDictionary<string, decimal> allocatedAmountsByInvoiceId,
            int maxResults = 4)
            => Array.Empty<BankReconciliationRecommendationItem>();

        public BankReconciliationAutoMatchResult BuildAutoMatches(
            IReadOnlyList<BankReconciliationTransactionCandidate> transactions,
            IReadOnlyList<InvoiceItem> invoices)
        {
            LastAutoTransactions = transactions;
            LastAutoInvoices = invoices;
            return AutoMatches;
        }

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
        {
            ReplaceCallCount += 1;
            ReplacedMatches = matches;
            State.Matches = matches.ToList();
            State.Version += 1;
            return Task.FromResult(State);
        }

        public Task<BankReconciliationPersistedState> UpsertMatchAsync(
            Guid companyId,
            string stateKey,
            UserSession? user,
            BankReconciliationSavedMatch match,
            int? expectedVersion = null,
            string? note = null,
            CancellationToken cancellationToken = default)
        {
            if (ConflictVersion is int currentVersion)
            {
                throw new BankReconciliationStateConflictException(currentVersion);
            }

            SavedMatch = match;
            State.Version = 2;
            return Task.FromResult(State);
        }

        public int? ConflictVersion { get; init; }

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

    private sealed class FakePaymentBundleMatcher : IBankReconciliationPaymentBundleMatcher
    {
        public List<BankReconciliationPaymentBundleSuggestion> Suggestions { get; } = new();
        public IReadOnlyList<BankReconciliationSavedMatch> LastExistingMatches { get; private set; } = Array.Empty<BankReconciliationSavedMatch>();

        public IReadOnlyList<BankReconciliationPaymentBundleSuggestion> BuildSuggestions(
            IReadOnlyList<BankReconciliationTransactionCandidate> transactions,
            IReadOnlyList<InvoiceItem> invoices,
            IReadOnlyList<BankReconciliationSavedMatch> existingMatches)
        {
            LastExistingMatches = existingMatches;
            return Suggestions;
        }
    }

    private sealed class FakeInvoiceCandidateService : IBankReconciliationInvoiceCandidateService
    {
        private readonly List<InvoiceItem> _invoices;

        public FakeInvoiceCandidateService(IEnumerable<InvoiceItem> invoices)
        {
            _invoices = invoices.ToList();
        }

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
            => Task.FromResult(new BankReconciliationInvoiceCandidateResult { Invoices = _invoices });

        public Task<BankReconciliationInvoiceCandidateResult> LoadCustomerPageAsync(
            UserSession user,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
            => Task.FromResult(new BankReconciliationInvoiceCandidateResult { Invoices = _invoices });

        public Task<BankReconciliationInvoiceCandidateResult> LoadSupplierPageAsync(
            UserSession user,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
            => Task.FromResult(new BankReconciliationInvoiceCandidateResult { Invoices = _invoices });
    }

    private sealed class DummyStringLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }
}

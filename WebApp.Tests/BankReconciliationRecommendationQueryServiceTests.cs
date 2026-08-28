using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;
using WebApp.Services.Integration.BankReconciliation;
using WebApp.Services.Integration.BankReconciliation.Invoices;
using WebApp.Services.Integration.BankReconciliation.Queries;

namespace WebApp.Tests;

// Recommendation query tests cover read-side allocation logic without MVC.
public sealed class BankReconciliationRecommendationQueryServiceTests
{
    [Fact]
    public async Task BuildRecommendations_UsesRemainingTransactionAmountAndExternalInvoiceAllocations()
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
                        InvoiceId = "INV-A",
                        MatchedAmount = 40m
                    },
                    new BankReconciliationSavedMatch
                    {
                        TransactionId = "TX-2",
                        InvoiceId = "INV-1",
                        MatchedAmount = 25m
                    }
                }
            }
        };
        var invoiceService = new FakeInvoiceCandidateService(new[]
        {
            new InvoiceItem
            {
                InvoiceNo = "INV-1",
                AmountSek = 100m,
                RemainingAmount = 100m,
                DueDate = new DateTime(2026, 5, 31)
            }
        });
        var service = CreateService(bankService, invoiceService);

        var result = await service.BuildRecommendationsAsync(
            new BankReconciliationSourceContext
            {
                StateKey = "state-1",
                Transactions =
                {
                    new BankReconciliationParsedTransaction
                    {
                        Id = "TX-1",
                        Amount = 100m,
                        Currency = "SEK"
                    }
                }
            },
            new UserSession
            {
                UserId = "user-1",
                CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111")
            },
            "TX-1",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Items);
        Assert.Equal(60m, bankService.LastTransactionAmount);
        Assert.Equal(25m, bankService.LastAllocatedAmounts["INV-1"]);
    }

    [Fact]
    public async Task BuildAiSuggestions_ReturnsSupportReferenceAndLogsSanitizedFailure()
    {
        var logger = new CapturingLogger<BankReconciliationRecommendationQueryService>();
        var bankService = new FakeBankReconciliationService { ThrowOnAiSuggestions = true };
        var service = CreateService(
            bankService,
            new FakeInvoiceCandidateService(
            [
                new InvoiceItem
                {
                    InvoiceNo = "INV-1",
                    AmountSek = 100m,
                    RemainingAmount = 100m,
                    DueDate = new DateTime(2026, 5, 31)
                }
            ]),
            logger);

        var result = await service.BuildAiSuggestionsAsync(
            new BankReconciliationSourceContext
            {
                StateKey = "state-1",
                Transactions =
                {
                    new BankReconciliationParsedTransaction { Id = "TX-1", Amount = 100m, Currency = "SEK" }
                }
            },
            new UserSession
            {
                UserId = "user-1",
                CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111")
            },
            "TX-1",
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Referens:", result.ErrorMessage);
        Assert.DoesNotContain("authorization=secret-value", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        var errorLog = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Contains("SupportId=", errorLog.Message);
        Assert.DoesNotContain("authorization=secret-value", errorLog.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static BankReconciliationRecommendationQueryService CreateService(
        IBankReconciliationService bankService,
        IBankReconciliationInvoiceCandidateService invoiceService,
        ILogger<BankReconciliationRecommendationQueryService>? logger = null)
        => new(
            bankService,
            invoiceService,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationRecommendationQueryService>.Instance);

    private sealed class FakeBankReconciliationService : IBankReconciliationService
    {
        public BankReconciliationPersistedState State { get; set; } = new();
        public decimal LastTransactionAmount { get; private set; }
        public IReadOnlyDictionary<string, decimal> LastAllocatedAmounts { get; private set; } = new Dictionary<string, decimal>();
        public bool ThrowOnAiSuggestions { get; init; }

        public IReadOnlyList<BankReconciliationRecommendationItem> BuildRecommendations(
            BankReconciliationTransactionCandidate transaction,
            IReadOnlyList<InvoiceItem> invoices,
            IReadOnlyDictionary<string, decimal> allocatedAmountsByInvoiceId,
            int maxResults = 4)
        {
            LastTransactionAmount = transaction.Amount;
            LastAllocatedAmounts = allocatedAmountsByInvoiceId;
            return new[]
            {
                new BankReconciliationRecommendationItem
                {
                    Invoice = new BankReconciliationRecommendationInvoice
                    {
                        InvoiceNo = invoices[0].InvoiceNo
                    }
                }
            };
        }

        public BankReconciliationAutoMatchResult BuildAutoMatches(
            IReadOnlyList<BankReconciliationTransactionCandidate> transactions,
            IReadOnlyList<InvoiceItem> invoices)
            => new();

        public Task<BankReconciliationAiSuggestionResult> BuildAiSuggestionsAsync(
            BankReconciliationAiSuggestionRequest request,
            CancellationToken cancellationToken = default)
            => ThrowOnAiSuggestions
                ? throw new InvalidOperationException("authorization=secret-value")
                : Task.FromResult(new BankReconciliationAiSuggestionResult());

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

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }

    private sealed record LogEntry(LogLevel Level, string Message);
}

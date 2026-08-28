using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebApp.Models.Integration;
using WebApp.Services.Integration.BankReconciliation.Invoices;
using WebApp.Services.Integration.BankReconciliation.Presentation;

namespace WebApp.Services.Integration.BankReconciliation.Queries;

// Prepares transaction recommendation data without coupling the rules to MVC.
public sealed class BankReconciliationRecommendationQueryService : IBankReconciliationRecommendationQueryService
{
    private readonly IBankReconciliationService _bankReconciliationService;
    private readonly IBankReconciliationInvoiceCandidateService _invoiceCandidateService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<BankReconciliationRecommendationQueryService> _logger;

    public BankReconciliationRecommendationQueryService(
        IBankReconciliationService bankReconciliationService,
        IBankReconciliationInvoiceCandidateService invoiceCandidateService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<BankReconciliationRecommendationQueryService> logger)
    {
        _bankReconciliationService = bankReconciliationService;
        _invoiceCandidateService = invoiceCandidateService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<BankReconciliationRecommendationQueryResult> BuildRecommendationsAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        string? transactionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transactionId) || !HasActiveState(source, user))
        {
            return new BankReconciliationRecommendationQueryResult();
        }

        var context = await ResolveRecommendationContextAsync(source, user!, transactionId, cancellationToken);
        if (!context.Success)
        {
            return new BankReconciliationRecommendationQueryResult
            {
                Success = false,
                ErrorMessage = context.ErrorMessage
            };
        }

        return new BankReconciliationRecommendationQueryResult
        {
            Success = true,
            Items = context.Recommendations
        };
    }

    public async Task<BankReconciliationAiSuggestionQueryResult> BuildAiSuggestionsAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        string? transactionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transactionId) || !HasActiveState(source, user))
        {
            return new BankReconciliationAiSuggestionQueryResult();
        }

        try
        {
            var context = await ResolveRecommendationContextAsync(source, user!, transactionId, cancellationToken);
            if (!context.Success)
            {
                return new BankReconciliationAiSuggestionQueryResult
                {
                    Success = false,
                    ErrorMessage = context.ErrorMessage
                };
            }

            var result = await _bankReconciliationService.BuildAiSuggestionsAsync(
                new BankReconciliationAiSuggestionRequest
                {
                    CompanyId = user!.CompanyId!.Value,
                    StateKey = source.StateKey!,
                    RequestedByUserId = user.UserId,
                    Transaction = context.TransactionCandidate!,
                    RuleCandidates = context.Recommendations
                },
                cancellationToken);

            return new BankReconciliationAiSuggestionQueryResult
            {
                Success = true,
                Result = result
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new BankReconciliationAiSuggestionQueryResult
            {
                Success = false,
                ErrorMessage = BankReconciliationErrorHandling.LogAndBuildUserMessage(
                    _logger,
                    _httpContextAccessor.HttpContext,
                    "BuildBankReconciliationAiSuggestions",
                    "AI-förslaget kunde inte hämtas just nu. Regelmotorns rekommendationer påverkas inte.",
                    ex)
            };
        }
    }

    private async Task<RecommendationContext> ResolveRecommendationContextAsync(
        BankReconciliationSourceContext source,
        UserSession user,
        string transactionId,
        CancellationToken cancellationToken)
    {
        var transaction = source.Transactions.FirstOrDefault(x => string.Equals(x.Id, transactionId, StringComparison.OrdinalIgnoreCase));
        if (transaction is null)
        {
            return RecommendationContext.Failure("Transaktionen kunde inte hittas.");
        }

        var invoicesResult = await _invoiceCandidateService.LoadAsync(
            source.IsDemoMode,
            user,
            cancellationToken,
            transaction,
            demoScenarioKey: source.DemoScenarioKey);
        if (!string.IsNullOrWhiteSpace(invoicesResult.ErrorMessage))
        {
            return RecommendationContext.Failure(invoicesResult.ErrorMessage);
        }

        var state = await _bankReconciliationService.LoadStateAsync(user.CompanyId!.Value, source.StateKey!, cancellationToken);
        var allocatedByInvoice = BuildAllocatedAmountsByInvoice(state.Matches, transactionId);
        var transactionAllocated = state.Matches
            .Where(x => string.Equals(x.TransactionId, transactionId, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.MatchedAmount);

        var transactionCandidate = BankReconciliationTransactionPageService.MapTransactionCandidate(transaction);
        transactionCandidate.Amount = Math.Max(GetMatchableAmount(transaction) - transactionAllocated, 0m);
        var recommendations = _bankReconciliationService
            .BuildRecommendations(transactionCandidate, invoicesResult.Invoices, allocatedByInvoice)
            .ToList();

        return RecommendationContext.SuccessResult(transactionCandidate, recommendations);
    }

    private static Dictionary<string, decimal> BuildAllocatedAmountsByInvoice(
        IEnumerable<BankReconciliationSavedMatch> matches,
        string transactionId)
    {
        return matches
            .Where(x => string.Equals(x.TransactionId, transactionId, StringComparison.OrdinalIgnoreCase) == false)
            .GroupBy(x => x.InvoiceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.MatchedAmount), StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasActiveState(BankReconciliationSourceContext source, UserSession? user)
        => user?.CompanyId is Guid companyId && companyId != Guid.Empty && !string.IsNullOrWhiteSpace(source.StateKey);

    private static decimal GetMatchableAmount(BankReconciliationParsedTransaction transaction)
        => Math.Abs(transaction.Amount);

    private sealed class RecommendationContext
    {
        public bool Success { get; private init; }
        public string? ErrorMessage { get; private init; }
        public BankReconciliationTransactionCandidate? TransactionCandidate { get; private init; }
        public List<BankReconciliationRecommendationItem> Recommendations { get; private init; } = new();

        public static RecommendationContext SuccessResult(
            BankReconciliationTransactionCandidate transactionCandidate,
            List<BankReconciliationRecommendationItem> recommendations)
            => new()
            {
                Success = true,
                TransactionCandidate = transactionCandidate,
                Recommendations = recommendations
            };

        public static RecommendationContext Failure(string? errorMessage)
            => new()
            {
                Success = false,
                ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "Rekommendationer kunde inte hämtas." : errorMessage
            };
    }
}

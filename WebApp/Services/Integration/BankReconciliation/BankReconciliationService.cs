using Entities.Application;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;

namespace WebApp.Services.Integration.BankReconciliation;

// Facade for bank reconciliation use cases across matching, state, and AI suggestion flows.
public sealed class BankReconciliationService : IBankReconciliationService
{
    private readonly IBankReconciliationAiSuggestionService _aiSuggestionService;
    private readonly IBankReconciliationMatchingService _matchingService;
    private readonly IBankReconciliationStateService _stateService;

    public BankReconciliationService(
        IBankReconciliationAiSuggestionService aiSuggestionService,
        IBankReconciliationMatchingService matchingService,
        IBankReconciliationStateService stateService)
    {
        _aiSuggestionService = aiSuggestionService;
        _matchingService = matchingService;
        _stateService = stateService;
    }

    public IReadOnlyList<BankReconciliationRecommendationItem> BuildRecommendations(
        BankReconciliationTransactionCandidate transaction,
        IReadOnlyList<InvoiceItem> invoices,
        IReadOnlyDictionary<string, decimal> allocatedAmountsByInvoiceId,
        int maxResults = 4)
        => _matchingService.BuildRecommendations(transaction, invoices, allocatedAmountsByInvoiceId, maxResults);

    public BankReconciliationAutoMatchResult BuildAutoMatches(
        IReadOnlyList<BankReconciliationTransactionCandidate> transactions,
        IReadOnlyList<InvoiceItem> invoices)
        => _matchingService.BuildAutoMatches(transactions, invoices);

    public async Task<BankReconciliationAiSuggestionResult> BuildAiSuggestionsAsync(
        BankReconciliationAiSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!CanUseInvoiceMatching(request.Transaction))
        {
            return new BankReconciliationAiSuggestionResult
            {
                Enabled = true,
                Status = "skipped-coding-rule",
                Message = "AI kördes inte eftersom transaktionens kodning inte ska matchas mot faktura.",
                Suggestions = new List<BankReconciliationAiSuggestionCandidate>()
            };
        }

        if (HasStrongDeterministicRecommendation(request.RuleCandidates))
        {
            return new BankReconciliationAiSuggestionResult
            {
                Enabled = true,
                Status = "skipped-strong-rule-match",
                Message = "AI kördes inte eftersom regelmotorn redan har en stark träff.",
                Suggestions = new List<BankReconciliationAiSuggestionCandidate>()
            };
        }

        return await _aiSuggestionService.BuildSuggestionsAsync(request, cancellationToken);
    }

    public Task<BankReconciliationPersistedState> LoadStateAsync(
        Guid companyId,
        string stateKey,
        CancellationToken cancellationToken = default)
        => _stateService.LoadAsync(companyId, stateKey, cancellationToken);

    public Task<BankReconciliationPersistedState> ReplaceMatchesAsync(
        Guid companyId,
        string stateKey,
        UserSession? user,
        IReadOnlyList<BankReconciliationSavedMatch> matches,
        string auditActionType,
        int? expectedVersion = null,
        string? note = null,
        CancellationToken cancellationToken = default)
        => _stateService.ReplaceMatchesAsync(companyId, stateKey, user, matches, auditActionType, expectedVersion, note, cancellationToken);

    public Task<BankReconciliationPersistedState> UpsertMatchAsync(
        Guid companyId,
        string stateKey,
        UserSession? user,
        BankReconciliationSavedMatch match,
        int? expectedVersion = null,
        string? note = null,
        CancellationToken cancellationToken = default)
        => _stateService.UpsertMatchAsync(companyId, stateKey, user, match, expectedVersion, note, cancellationToken);

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
        => _stateService.ReverseMatchAsync(companyId, stateKey, user, transactionId, allocationId, invoiceId, expectedVersion, reason, cancellationToken);

    private static bool HasStrongDeterministicRecommendation(IReadOnlyList<BankReconciliationRecommendationItem> recommendations)
    {
        var strongCandidates = recommendations
            .Where(item => item.RequiresManualConfirmation == false
                           && item.Confidence.Score >= 90
                           && item.RuleKey.Contains("ref-exact", StringComparison.Ordinal)
                           && item.RuleKey.Contains("amount-exact", StringComparison.Ordinal))
            .Take(2)
            .ToList();

        return strongCandidates.Count == 1;
    }

    private static bool CanUseInvoiceMatching(BankReconciliationTransactionCandidate transaction)
    {
        if (Math.Abs(transaction.Amount) <= 0m)
            return false;

        var typeKey = (transaction.ResolvedCodingTypeKey ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(typeKey))
            return true;

        return typeKey is "bankinbetalningar" or "leverantorsbetalning" or "def";
    }
}

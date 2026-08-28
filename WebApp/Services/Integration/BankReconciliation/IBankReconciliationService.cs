using Entities.Application;
using WebApp.Models.Integration;
using WebApp.Models.Invoices;

namespace WebApp.Services.Integration.BankReconciliation;

// Coordinates bank reconciliation matching, state handling, and AI suggestion flow.
public interface IBankReconciliationService
{
    IReadOnlyList<BankReconciliationRecommendationItem> BuildRecommendations(
        BankReconciliationTransactionCandidate transaction,
        IReadOnlyList<InvoiceItem> invoices,
        IReadOnlyDictionary<string, decimal> allocatedAmountsByInvoiceId,
        int maxResults = 4);

    BankReconciliationAutoMatchResult BuildAutoMatches(
        IReadOnlyList<BankReconciliationTransactionCandidate> transactions,
        IReadOnlyList<InvoiceItem> invoices);

    Task<BankReconciliationAiSuggestionResult> BuildAiSuggestionsAsync(
        BankReconciliationAiSuggestionRequest request,
        CancellationToken cancellationToken = default);

    Task<BankReconciliationPersistedState> LoadStateAsync(
        Guid companyId,
        string stateKey,
        CancellationToken cancellationToken = default);

    Task<BankReconciliationPersistedState> ReplaceMatchesAsync(
        Guid companyId,
        string stateKey,
        UserSession? user,
        IReadOnlyList<BankReconciliationSavedMatch> matches,
        string auditActionType,
        int? expectedVersion = null,
        string? note = null,
        CancellationToken cancellationToken = default);

    Task<BankReconciliationPersistedState> UpsertMatchAsync(
        Guid companyId,
        string stateKey,
        UserSession? user,
        BankReconciliationSavedMatch match,
        int? expectedVersion = null,
        string? note = null,
        CancellationToken cancellationToken = default);

    Task<BankReconciliationPersistedState> ReverseMatchAsync(
        Guid companyId,
        string stateKey,
        UserSession? user,
        string transactionId,
        string? allocationId = null,
        string? invoiceId = null,
        int? expectedVersion = null,
        string? reason = null,
        CancellationToken cancellationToken = default);
}

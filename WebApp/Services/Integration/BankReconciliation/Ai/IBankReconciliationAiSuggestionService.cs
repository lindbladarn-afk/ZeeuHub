using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation;

// Produces AI-assisted bank reconciliation suggestions from rule-approved candidates only.
public interface IBankReconciliationAiSuggestionService
{
    Task<BankReconciliationAiSuggestionResult> BuildSuggestionsAsync(
        BankReconciliationAiSuggestionRequest request,
        CancellationToken cancellationToken = default);
}

using Entities.Application;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.Queries;

// Builds read-only recommendation and AI suggestion results for bank reconciliation transactions.
public interface IBankReconciliationRecommendationQueryService
{
    Task<BankReconciliationRecommendationQueryResult> BuildRecommendationsAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        string? transactionId,
        CancellationToken cancellationToken);

    Task<BankReconciliationAiSuggestionQueryResult> BuildAiSuggestionsAsync(
        BankReconciliationSourceContext source,
        UserSession? user,
        string? transactionId,
        CancellationToken cancellationToken);
}

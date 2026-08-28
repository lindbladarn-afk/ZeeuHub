using WebApp.Models.Integration;
using WebApp.Models.Invoices;

namespace WebApp.Services.Integration.BankReconciliation;

public interface IBankReconciliationMatchingService
{
    BankReconciliationMatchEligibilityResult EvaluateEligibility(
        BankReconciliationTransactionCandidate transaction,
        InvoiceItem invoice);

    IReadOnlyList<BankReconciliationRecommendationItem> BuildRecommendations(
        BankReconciliationTransactionCandidate transaction,
        IReadOnlyList<InvoiceItem> invoices,
        IReadOnlyDictionary<string, decimal> allocatedAmountsByInvoiceId,
        int maxResults = 4);

    BankReconciliationAutoMatchResult BuildAutoMatches(
        IReadOnlyList<BankReconciliationTransactionCandidate> transactions,
        IReadOnlyList<InvoiceItem> invoices);
}

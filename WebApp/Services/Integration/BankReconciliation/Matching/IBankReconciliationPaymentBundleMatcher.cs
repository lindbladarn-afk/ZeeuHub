using WebApp.Models.Integration;
using WebApp.Models.Invoices;

namespace WebApp.Services.Integration.BankReconciliation;

// Finds bounded many-to-one payment combinations from deterministic match evidence.
public interface IBankReconciliationPaymentBundleMatcher
{
    IReadOnlyList<BankReconciliationPaymentBundleSuggestion> BuildSuggestions(
        IReadOnlyList<BankReconciliationTransactionCandidate> transactions,
        IReadOnlyList<InvoiceItem> invoices,
        IReadOnlyList<BankReconciliationSavedMatch> existingMatches);
}

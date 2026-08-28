using WebApp.Models.Integration;
using WebApp.Models.Invoices;

namespace WebApp.Services.Integration.BankReconciliation;

// Defines the hard accounting rules that must pass before an invoice can be scored against a transaction.
public interface IBankReconciliationMatchEligibilityService
{
    BankReconciliationMatchEligibilityResult Evaluate(
        BankReconciliationTransactionCandidate transaction,
        InvoiceItem invoice);
}

using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.SupplierInvoices;

// Reads supplier payment invoice candidates from Jeeves for bank reconciliation.
public interface IBankReconciliationSupplierInvoiceRepository
{
    Task<BankReconciliationSupplierInvoiceResult> GetPaymentCandidatesAsync(
        string connectionString,
        BankReconciliationSupplierInvoiceQuery query,
        CancellationToken cancellationToken = default);
}

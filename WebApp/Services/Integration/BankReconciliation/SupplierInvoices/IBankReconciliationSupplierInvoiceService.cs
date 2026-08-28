using WebApp.Models.Invoices;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.SupplierInvoices;

// Provides supplier invoices in the existing bank reconciliation invoice shape.
public interface IBankReconciliationSupplierInvoiceService
{
    Task<(IReadOnlyList<InvoiceItem> Invoices, int TotalCount)> GetPaymentCandidatesAsync(
        string connectionString,
        BankReconciliationSupplierInvoiceQuery query,
        CancellationToken cancellationToken = default);
}

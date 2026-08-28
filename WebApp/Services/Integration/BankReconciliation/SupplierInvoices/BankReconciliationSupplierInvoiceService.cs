using WebApp.Models.Invoices;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation.SupplierInvoices;

// Maps Jeeves supplier-payment candidates to the shared invoice contract used by the matcher and UI.
public sealed class BankReconciliationSupplierInvoiceService : IBankReconciliationSupplierInvoiceService
{
    private readonly IBankReconciliationSupplierInvoiceRepository _repository;

    public BankReconciliationSupplierInvoiceService(IBankReconciliationSupplierInvoiceRepository repository)
    {
        _repository = repository;
    }

    public async Task<(IReadOnlyList<InvoiceItem> Invoices, int TotalCount)> GetPaymentCandidatesAsync(
        string connectionString,
        BankReconciliationSupplierInvoiceQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await _repository.GetPaymentCandidatesAsync(connectionString, query, cancellationToken);
        return (result.Invoices.Select(MapToInvoiceItem).ToList(), result.TotalCount);
    }

    internal static InvoiceItem MapToInvoiceItem(BankReconciliationSupplierInvoiceRow row)
    {
        var dueDate = row.PreferredPaymentDate ?? row.InvoiceDate ?? DateTime.Today;
        var currency = string.IsNullOrWhiteSpace(row.PaymentCurrencyCode) ? row.InvoiceCurrencyCode : row.PaymentCurrencyCode;
        var amount = Math.Abs(row.PaymentAmount == 0m ? row.InvoiceAmount : row.PaymentAmount);
        var invoiceAmount = Math.Abs(row.InvoiceAmount == 0m ? amount : row.InvoiceAmount);

        return new InvoiceItem
        {
            InvoiceNo = row.InvoiceNumber,
            Customer = row.PayeeName,
            SalesPerson = row.PayeeId,
            DueDate = dueDate,
            PaidDate = null,
            AmountSek = amount,
            AmountExclVat = invoiceAmount,
            PaidAmount = 0m,
            RemainingAmount = amount,
            Ocr = row.InvoiceNumber,
            Currency = string.IsNullOrWhiteSpace(currency) ? "SEK" : currency.Trim().ToUpperInvariant(),
            CompanyCode = row.CompanyCode.ToString(),
            IsSupplierInvoice = true,
            IsPaid = false,
            Status = dueDate.Date < DateTime.Today ? "Förfallen" : "Obetald"
        };
    }
}

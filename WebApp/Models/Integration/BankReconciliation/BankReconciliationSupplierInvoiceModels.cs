namespace WebApp.Models.Integration;

// Supplier invoice rows used as bank reconciliation candidates for outgoing payments.
public sealed class BankReconciliationSupplierInvoiceQuery
{
    public int? CompanyCode { get; set; }
    public int? PaymentJournalNumber { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? SourceTimestamp { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

public sealed class BankReconciliationSupplierInvoiceRow
{
    public int CompanyCode { get; set; }
    public int PaymentJournalNumber { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string PayeeId { get; set; } = string.Empty;
    public string PayeeName { get; set; } = string.Empty;
    public decimal PaymentAmount { get; set; }
    public string PaymentCurrencyCode { get; set; } = "SEK";
    public DateTime? InvoiceDate { get; set; }
    public DateTime? PreferredPaymentDate { get; set; }
    public decimal InvoiceAmount { get; set; }
    public string InvoiceCurrencyCode { get; set; } = "SEK";
}

public sealed class BankReconciliationSupplierInvoiceResult
{
    public IReadOnlyList<BankReconciliationSupplierInvoiceRow> Invoices { get; set; } = Array.Empty<BankReconciliationSupplierInvoiceRow>();
    public int TotalCount { get; set; }
}

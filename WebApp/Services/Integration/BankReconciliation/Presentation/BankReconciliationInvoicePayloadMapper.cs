using WebApp.Models.Integration;
using WebApp.Models.Invoices;

namespace WebApp.Services.Integration.BankReconciliation.Presentation;

// Maps invoice sources into the JSON payload used by bank reconciliation screens.
public static class BankReconciliationInvoicePayloadMapper
{
    public static BankReconciliationInvoicePayload MapInvoice(InvoiceItem invoice)
        => new()
        {
            Id = invoice.InvoiceNo,
            InvoiceNo = invoice.InvoiceNo,
            Ocr = string.IsNullOrWhiteSpace(invoice.Ocr) ? null : invoice.Ocr,
            CustomerName = invoice.Customer,
            Amount = invoice.AmountSek,
            Currency = "SEK",
            DueDate = invoice.DueDate.ToString("yyyy-MM-dd"),
            IsDemo = false
        };

    public static BankReconciliationInvoicePayload MapDemoInvoice(BankReconciliationDemoInvoice invoice)
        => new()
        {
            Id = string.IsNullOrWhiteSpace(invoice.Id) ? invoice.InvoiceNo ?? string.Empty : invoice.Id,
            InvoiceNo = invoice.InvoiceNo,
            Ocr = invoice.Ocr,
            CustomerName = invoice.CustomerName,
            Amount = invoice.Amount,
            Currency = string.IsNullOrWhiteSpace(invoice.Currency) ? "SEK" : invoice.Currency,
            DueDate = invoice.DueDate,
            IsDemo = true
        };
}

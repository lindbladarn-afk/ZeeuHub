using System;

namespace WebApp.Models.Invoices;

// Holds the shared invoice row used by invoice, dashboard and bank-reconciliation flows.
public class InvoiceItem
{
    public string InvoiceNo { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public string SalesPerson { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public decimal AmountSek { get; set; }
    public decimal AmountExclVat { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Ocr { get; set; } = string.Empty;
    public string Currency { get; set; } = "SEK";
    public string CompanyCode { get; set; } = string.Empty;
    public bool IsSupplierInvoice { get; set; }
    public bool IsPaid { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsOverdue => !IsPaid && DueDate.Date < DateTime.Today;
}

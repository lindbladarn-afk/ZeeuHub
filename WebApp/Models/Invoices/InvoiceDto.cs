using System;

namespace WebApp.Models.Invoices
{
    public class InvoiceDto
    {
        public string InvoiceNo { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string SalesPerson { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? PaidDate { get; set; }
        public decimal AmountInclVat { get; set; }
        public decimal AmountExclVat { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public string Ocr { get; set; } = string.Empty;
        public string CompanyCode { get; set; } = string.Empty;
    }

    public class InvoiceDashboardSummaryDto
    {
        public decimal TotalUnpaidSek { get; set; }
        public decimal TotalPaidSek { get; set; }
        public int UnpaidCount { get; set; }
        public bool UsesHistoricalFactSource { get; set; }
        public IReadOnlyList<InvoiceDto> OverdueInvoices { get; set; } = Array.Empty<InvoiceDto>();
    }

    public class PagedInvoicesResultDto
    {
        public IReadOnlyList<InvoiceDto> Invoices { get; set; } = Array.Empty<InvoiceDto>();
        public int TotalCount { get; set; }
        public decimal TotalUnpaidSek { get; set; }
        public decimal TotalPaidSek { get; set; }
        public int UnpaidCount { get; set; }
        public int PaidCount { get; set; }
        public int OverdueCount { get; set; }
        public bool UsesHistoricalFactSource { get; set; }
    }
}

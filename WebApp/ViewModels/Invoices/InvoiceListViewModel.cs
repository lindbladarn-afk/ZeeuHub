using System;
using System.Collections.Generic;
using WebApp.Models.Invoices;

namespace WebApp.ViewModels.Invoices;

// Holds the invoice list page state and its monthly summary rows.
public class InvoiceMonthlySummary
{
    public string Label { get; set; } = string.Empty;
    public decimal PaidAmountSek { get; set; }
    public decimal UnpaidAmountSek { get; set; }
    public int PaidCount { get; set; }
    public int UnpaidCount { get; set; }
}

public class InvoiceListViewModel
{
    public IReadOnlyList<InvoiceItem> UnpaidInvoices { get; set; } = Array.Empty<InvoiceItem>();
    public IReadOnlyList<InvoiceItem> PaidInvoices { get; set; } = Array.Empty<InvoiceItem>();
    public IReadOnlyList<InvoiceItem> OverdueHighlights { get; set; } = Array.Empty<InvoiceItem>();
    public IReadOnlyList<InvoiceMonthlySummary> Monthly { get; set; } = Array.Empty<InvoiceMonthlySummary>();
    public string? Search { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string ActiveTab { get; set; } = "unpaid";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; } = 1;
    public int? SelectedYear { get; set; }
    public IReadOnlyList<int> AvailableYears { get; set; } = Array.Empty<int>();
    public bool UsesDefaultPeriod { get; set; }
    public bool UsesHistoricalFactSource { get; set; }
    public string? DataSourceNotice { get; set; }

    public decimal TotalUnpaidSek { get; set; }
    public decimal TotalPaidSek { get; set; }
    public int UnpaidCount { get; set; }
    public int PaidCount { get; set; }
    public int OverdueCount { get; set; }
}

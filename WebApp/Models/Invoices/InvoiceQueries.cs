using System;
using System.Collections.Generic;

namespace WebApp.Models.Invoices
{
    public sealed class GetInvoicesQuery
    {
        public int? CompanyCode { get; set; }
        public string? Search { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string ActiveTab { get; set; } = "unpaid";
        public int? Page { get; set; }
        public int? PageSize { get; set; }
        public int? SelectedYear { get; set; }
        public IReadOnlyList<int>? AvailableYears { get; set; }
        public bool UsesDefaultPeriod { get; set; }
    }

    public sealed class GetInvoiceQuery
    {
        public int? CompanyCode { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
    }
}

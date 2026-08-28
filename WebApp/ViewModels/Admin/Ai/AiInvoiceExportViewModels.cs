using System;
using System.Collections.Generic;

namespace WebApp.ViewModels.Admin;

/// <summary>
/// View models for AI invoice export tab in admin portal.
/// Keeps invoice/export presentation separate from quota-policy configuration models.
/// </summary>
public class AiInvoiceExportVm
{
    public int SelectedYear { get; set; }
    public int SelectedMonth { get; set; }
    public IReadOnlyCollection<AiInvoiceExportRowVm> Rows { get; set; } = Array.Empty<AiInvoiceExportRowVm>();
}

public class AiInvoiceExportRowVm
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = "-";
    public int UsedTokens { get; set; }
    public int ExtraTokens { get; set; }
    public decimal BaseCostSek { get; set; }
    public decimal SurchargeSek { get; set; }
    public decimal TotalBillableSek { get; set; }
}

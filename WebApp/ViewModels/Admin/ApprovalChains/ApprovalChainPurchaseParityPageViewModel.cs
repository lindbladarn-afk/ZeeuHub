using System.ComponentModel.DataAnnotations;
using WebApp.Services.Admin.ApprovalChains;

namespace WebApp.ViewModels.Admin.ApprovalChains;

// Form model for the internal parity check that compares portal rules against Jeeves purchase approval behavior.
public sealed class ApprovalChainPurchaseParityPageViewModel
{
    [Required]
    [Display(Name = "Företagskod")]
    public short? CompanyCode { get; set; }

    [Required]
    [Display(Name = "Beställningsnummer")]
    public long? PurchaseOrderNumber { get; set; }

    [Range(0, int.MaxValue)]
    [Display(Name = "FlowId")]
    public int FlowId { get; set; } = 0;

    [Required]
    [Display(Name = "Aktiv attestant")]
    public string CurrentApproverPersSign { get; set; } = string.Empty;

    public string? RuntimeCompanyName { get; set; }

    public ApprovalChainPurchaseParityResult? Result { get; set; }
}

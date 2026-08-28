namespace Entities.ViewModels.WebApproval;

public class WebApprovalSaleOrderAnonymousApprovalDto
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public string Action { get; set; }

    public string? Message { get; set; } = null;
}

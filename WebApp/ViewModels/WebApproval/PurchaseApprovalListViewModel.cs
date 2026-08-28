using Entities.ViewModels.WebApproval;

namespace WebApp.ViewModels.WebApproval;

// Carries the selected purchase approval status together with the rows shown in the list.
public sealed class PurchaseApprovalListViewModel
{
    public int? SelectedStatus { get; init; }
    public IReadOnlyList<WebApprovalPurchaseOrderVM> Orders { get; init; } = Array.Empty<WebApprovalPurchaseOrderVM>();
    public bool ShowsApproved => SelectedStatus == 1;
    public bool ShowsRejected => SelectedStatus == 2;
    public bool ShowsActive => SelectedStatus is null;
}

using Entities.ViewModels.WebApproval;

namespace WebApp.Services.WebApproval;

// Keeps purchase approval list filtering consistent between active work and decision history.
public static class PurchaseApprovalListFilter
{
    public static int? NormalizeHistoryStatus(int? status)
        => status is 1 or 2 ? status : null;

    public static IReadOnlyList<WebApprovalPurchaseOrderVM> ForCurrentUser(
        IEnumerable<WebApprovalPurchaseOrderVM>? orders,
        string currentPersSign,
        int? selectedStatus)
    {
        if (orders is null || string.IsNullOrWhiteSpace(currentPersSign))
            return Array.Empty<WebApprovalPurchaseOrderVM>();

        var normalizedStatus = NormalizeHistoryStatus(selectedStatus);
        return orders
            .Where(order => normalizedStatus is null
                ? IsActiveForCurrentAttestant(order, currentPersSign)
                : IsHandledByCurrentUser(order, currentPersSign, normalizedStatus.Value))
            .OrderByDescending(order => normalizedStatus is null ? order.OrderRegisteredDate : order.ApprovedDate ?? order.OrderRegisteredDate)
            .ToList();
    }

    private static bool IsActiveForCurrentAttestant(WebApprovalPurchaseOrderVM order, string currentPersSign)
    {
        return order.IsActive
            && (order.ApprovalStatus == 0 || order.ApprovalStatus == 3)
            && EqualsPersSign(order.AttestantPersSign, currentPersSign);
    }

    private static bool IsHandledByCurrentUser(WebApprovalPurchaseOrderVM order, string currentPersSign, int status)
    {
        return order.ApprovalStatus == status
            && (string.IsNullOrWhiteSpace(order.ApprovedBy)
                || EqualsPersSign(order.ApprovedBy, currentPersSign)
                || EqualsPersSign(order.AttestantPersSign, currentPersSign));
    }

    private static bool EqualsPersSign(string? left, string? right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}

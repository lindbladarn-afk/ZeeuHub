using Entities.ViewModels.WebApproval;
using WebApp.Services.WebApproval;

namespace WebApp.Tests;

// Verifies the purchase approval list separates active assignments from handled decision history.
public sealed class PurchaseApprovalListFilterTests
{
    [Fact]
    public void ForCurrentUser_DefaultMode_Returns_Active_Assignments()
    {
        var rows = new[]
        {
            Order("A", "zuaek", null, 0, true),
            Order("B", "other", null, 0, true),
            Order("C", "zuaek", "zuaek", 1, false)
        };

        var result = PurchaseApprovalListFilter.ForCurrentUser(rows, "ZUAEK", null);

        var item = Assert.Single(result);
        Assert.Equal("A", item.OrderNumber);
    }

    [Fact]
    public void ForCurrentUser_ApprovedMode_Returns_Handled_Approvals_By_User()
    {
        var rows = new[]
        {
            Order("A", "zuaek", "zuaek", 1, false),
            Order("B", "other", "other", 1, false),
            Order("C", "zuaek", "zuaek", 2, false)
        };

        var result = PurchaseApprovalListFilter.ForCurrentUser(rows, "zuaek", 1);

        var item = Assert.Single(result);
        Assert.Equal("A", item.OrderNumber);
    }

    [Fact]
    public void ForCurrentUser_ApprovedMode_Keeps_JeevesRows_When_ApprovedBy_Is_Missing()
    {
        var rows = new[]
        {
            Order("A", "zuaek", null, 1, false)
        };

        var result = PurchaseApprovalListFilter.ForCurrentUser(rows, "zuaek", 1);

        var item = Assert.Single(result);
        Assert.Equal("A", item.OrderNumber);
    }

    [Fact]
    public void ForCurrentUser_RejectedMode_Returns_Handled_Rejections_By_User()
    {
        var rows = new[]
        {
            Order("A", "zuaek", "zuaek", 2, false),
            Order("B", "zuaek", "zuaek", 1, false)
        };

        var result = PurchaseApprovalListFilter.ForCurrentUser(rows, "zuaek", 2);

        var item = Assert.Single(result);
        Assert.Equal("A", item.OrderNumber);
    }

    private static WebApprovalPurchaseOrderVM Order(
        string orderNumber,
        string attestantPersSign,
        string? approvedBy,
        int approvalStatus,
        bool isActive)
    {
        return new WebApprovalPurchaseOrderVM
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            AttestantPersSign = attestantPersSign,
            ApprovedBy = approvedBy,
            ApprovalStatus = approvalStatus,
            IsActive = isActive,
            OrderRegisteredDate = DateTime.UtcNow,
            ApprovedDate = approvalStatus is 1 or 2 ? DateTime.UtcNow : null
        };
    }
}

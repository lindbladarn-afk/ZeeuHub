using Entities.Application;
using Entities.ViewModels.WebApproval;
using Repository.Contracts;
using WebApp.Models.ActionCenter;
using WebApp.Services.ActionCenter;
using WebApp.Services.Application;

namespace WebApp.Tests;

// Verifies purchase approvals become actionable Action Center notifications for the assigned approver.
public sealed class PurchaseApprovalInsightProviderTests
{
    [Fact]
    public async Task GetInsightsAsync_Returns_One_Insight_Per_Active_Assigned_Purchase_Approval()
    {
        var companyId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var provider = new PurchaseApprovalInsightProvider(new FakePurchaseApprovalRepository(
            Order(firstId, "600002", "Office Depot Svenska AB", "ZUAEK", isActive: true, registeredDate: new DateTime(2026, 7, 1), orderValue: 1500),
            Order(Guid.NewGuid(), "600003", "Other Supplier", "OTHER", isActive: true, registeredDate: new DateTime(2026, 7, 1), orderValue: 2000),
            Order(Guid.NewGuid(), "600004", "Inactive Supplier", "ZUAEK", isActive: false, registeredDate: new DateTime(2026, 7, 1), orderValue: 3000),
            Order(secondId, "600005", "Office Depot Svenska AB", "zuaek", isActive: true, registeredDate: new DateTime(2026, 7, 2), orderValue: 4000)));

        var result = (await provider.GetInsightsAsync(
            new UserSession
            {
                Email = "alexander@example.com",
                PersSign = "zuaek",
                JeevesActiveCompany = 9900
            },
            new JeevesRuntimeContext
            {
                ConnectionString = "Server=jeeves;",
                CompanyCode = 9900,
                CompanyId = companyId,
                Email = "alexander@example.com",
                PersSign = "zuaek"
            },
            CancellationToken.None)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal($"purchase-approval:{firstId:N}", result[0].Key);
        Assert.Equal("Inköpsorder 600002 väntar på godkännande", result[0].Title);
        Assert.Equal($"/WebApproval/PurchaseApprovalDetails/{companyId}/{firstId}", result[0].LinkUrl);
        Assert.Equal("Attest", result[0].Category);
        Assert.Equal(ActionCenterPriority.Medium, result[0].Priority);
        Assert.Contains("Office Depot Svenska AB", result[0].Description);
        Assert.Equal($"purchase-approval:{secondId:N}", result[1].Key);
    }

    [Fact]
    public async Task GetInsightsAsync_Returns_Empty_When_User_Cannot_Be_Matched_To_Jeeves_Approver()
    {
        var provider = new PurchaseApprovalInsightProvider(new FakePurchaseApprovalRepository(
            Order(Guid.NewGuid(), "600002", "Office Depot Svenska AB", "ZUAEK", isActive: true, registeredDate: new DateTime(2026, 7, 1), orderValue: 1500)));

        var result = await provider.GetInsightsAsync(
            new UserSession
            {
                Email = "alexander@example.com",
                PersSign = string.Empty,
                JeevesActiveCompany = 9900
            },
            new JeevesRuntimeContext
            {
                ConnectionString = "Server=jeeves;",
                CompanyCode = 9900,
                CompanyId = Guid.NewGuid()
            },
            CancellationToken.None);

        Assert.Empty(result);
    }

    private static WebApprovalPurchaseOrderVM Order(
        Guid id,
        string orderNumber,
        string supplierName,
        string attestantPersSign,
        bool isActive,
        DateTime registeredDate,
        decimal orderValue)
    {
        return new WebApprovalPurchaseOrderVM
        {
            Id = id,
            OrderNumber = orderNumber,
            SupplierName = supplierName,
            AttestantPersSign = attestantPersSign,
            IsActive = isActive,
            OrderRegisteredDate = registeredDate,
            OrderValueLocal = orderValue,
            Currency = "SEK",
            OrderRows = new List<WebApprovalPurchaseOrderRowVM>()
        };
    }

    private sealed class FakePurchaseApprovalRepository : IWebApprovalPurchaseRepository
    {
        private readonly IReadOnlyList<WebApprovalPurchaseOrderVM> _orders;

        public FakePurchaseApprovalRepository(params WebApprovalPurchaseOrderVM[] orders)
        {
            _orders = orders;
        }

        public Task<IEnumerable<WebApprovalPurchaseOrderVM>> GetAllPurchaseAttestOrdersAsync(string connectionString, int? foretagKod, string emailAddress, int? status = null)
            => Task.FromResult<IEnumerable<WebApprovalPurchaseOrderVM>>(_orders);

        public Task<WebApprovalPurchaseOrderVM> GetAttestPurchaseOrderWithRowsAsync(string connectionString, Guid id)
            => throw new NotSupportedException();

        public Task UpdateOrderStatusAsync(string connectionString, Guid orderId, string attestStatus, string approvedBy, string? message = null)
            => throw new NotSupportedException();
    }
}

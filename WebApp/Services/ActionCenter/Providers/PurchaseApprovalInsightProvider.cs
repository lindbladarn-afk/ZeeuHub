using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Entities.Application;
using Entities.ViewModels.WebApproval;
using Repository.Contracts;
using WebApp.Models.ActionCenter;
using WebApp.Services.Application;

namespace WebApp.Services.ActionCenter;

// Builds Action Center items for purchase approvals assigned to the current Jeeves user.
public sealed class PurchaseApprovalInsightProvider : IInsightProvider
{
    private readonly IWebApprovalPurchaseRepository _purchaseRepo;

    public string ProviderKey => "customer-purchase-approvals";
    public ActionCenterAudience Audience => ActionCenterAudience.Customer;

    public PurchaseApprovalInsightProvider(IWebApprovalPurchaseRepository purchaseRepo)
    {
        _purchaseRepo = purchaseRepo;
    }

    public async Task<IEnumerable<ActionCenterInsight>> GetInsightsAsync(UserSession user, JeevesRuntimeContext? runtimeContext, CancellationToken cancellationToken)
    {
        if (runtimeContext is null)
            return Array.Empty<ActionCenterInsight>();

        var connectionString = runtimeContext.ConnectionString;
        var companyCode = runtimeContext.CompanyCode;

        if (string.IsNullOrWhiteSpace(connectionString))
            return Array.Empty<ActionCenterInsight>();

        if (string.IsNullOrWhiteSpace(user.Email) || string.IsNullOrWhiteSpace(user.PersSign))
            return Array.Empty<ActionCenterInsight>();

        var all = await _purchaseRepo.GetAllPurchaseAttestOrdersAsync(connectionString, companyCode, user.Email);
        var mine = all.Where(x => x.IsActive)
                      .Where(x => string.Equals(x.AttestantPersSign, user.PersSign, StringComparison.OrdinalIgnoreCase))
                      .OrderBy(x => x.OrderRegisteredDate)
                      .ThenBy(x => x.OrderNumber)
                      .ToList();

        if (mine.Count == 0)
            return Array.Empty<ActionCenterInsight>();

        return mine.Select(order => BuildInsight(order, runtimeContext)).ToArray();
    }

    private static ActionCenterInsight BuildInsight(WebApprovalPurchaseOrderVM order, JeevesRuntimeContext runtimeContext)
    {
        return new ActionCenterInsight
        {
            Key = $"purchase-approval:{order.Id:N}",
            Audience = ActionCenterAudience.Customer,
            Category = "Attest",
            Status = ActionCenterStatus.Open,
            Title = $"Inköpsorder {FormatValue(order.OrderNumber)} väntar på godkännande",
            Description = BuildDescription(order),
            Priority = ActionCenterPriority.Medium,
            DetectedAt = order.OrderRegisteredDate,
            DueAt = order.OrderRegisteredDate.AddDays(3),
            LinkText = "Öppna attest",
            LinkUrl = $"/WebApproval/PurchaseApprovalDetails/{runtimeContext.CompanyId}/{order.Id}",
            Metrics = BuildMetrics(order)
        };
    }

    private static string BuildDescription(WebApprovalPurchaseOrderVM order)
    {
        var supplier = FormatValue(order.SupplierName);
        var value = order.OrderValueLocal > 0
            ? $"{order.OrderValueLocal:N2} {FormatValue(order.Currency)}"
            : FormatValue(order.Currency);

        return $"Godkänn eller avvisa inköpsordern från {supplier}. Ordervärde: {value}.";
    }

    private static IReadOnlyList<ActionCenterMetric> BuildMetrics(WebApprovalPurchaseOrderVM order)
        => new[]
        {
            new ActionCenterMetric { Label = "Leverantör", Value = FormatValue(order.SupplierName) },
            new ActionCenterMetric { Label = "Ordervärde", Value = order.OrderValueLocal > 0 ? $"{order.OrderValueLocal:N2} {FormatValue(order.Currency)}" : "-" },
            new ActionCenterMetric { Label = "Registrerad", Value = order.OrderRegisteredDate.ToString("yyyy-MM-dd") }
        };

    private static string FormatValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}

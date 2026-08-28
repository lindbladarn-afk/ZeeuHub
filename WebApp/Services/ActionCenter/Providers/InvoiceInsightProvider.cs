using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Entities.Application;
using WebApp.Models.ActionCenter;
using WebApp.Models.Invoices;
using WebApp.Services.Application;
using WebApp.Services.Invoices;
using WebApp.ViewModels.Invoices;

namespace WebApp.Services.ActionCenter;

/// <summary>
/// Insights kopplade till fakturor (obetalt/förfallna).
/// </summary>
public sealed class InvoiceInsightProvider : IInsightProvider
{
    private readonly IInvoicesService _invoicesService;

    public string ProviderKey => "customer-invoices";
    public ActionCenterAudience Audience => ActionCenterAudience.Customer;

    public InvoiceInsightProvider(IInvoicesService invoicesService)
    {
        _invoicesService = invoicesService;
    }

    public async Task<IEnumerable<ActionCenterInsight>> GetInsightsAsync(UserSession user, JeevesRuntimeContext? runtimeContext, CancellationToken cancellationToken)
    {
        var connectionString = runtimeContext?.ConnectionString ?? string.Empty;
        var companyCode = runtimeContext?.CompanyCode ?? user.JeevesActiveCompany;

        if (string.IsNullOrWhiteSpace(connectionString) || companyCode == null)
            return Array.Empty<ActionCenterInsight>();

        var invoices = await _invoicesService.GetDashboardSummaryAsync(connectionString, companyCode);
        return BuildInvoiceInsights(invoices);
    }

    private static IEnumerable<ActionCenterInsight> BuildInvoiceInsights(InvoiceListViewModel invoices)
    {
        if (invoices.UnpaidCount <= 0)
            return Array.Empty<ActionCenterInsight>();

        var overdue = (invoices.UnpaidInvoices ?? Array.Empty<InvoiceItem>()).Where(x => x.IsOverdue).ToList();
        var maxOverdueDays = overdue.Count == 0 ? 0 : overdue.Max(x => (DateTime.Today - x.DueDate.Date).Days);
        var detectedAt = overdue.Count == 0 ? DateTime.Now : overdue.Min(x => x.DueDate);

        var title = invoices.UnpaidCount == 1 ? "1 obetald faktura" : $"{invoices.UnpaidCount} obetalda fakturor";
        var desc = overdue.Count > 0
            ? $"{invoices.UnpaidCount} st obetalda · {invoices.TotalUnpaidSek:N0} kr. Äldsta förfallna: {maxOverdueDays} dgr."
            : $"{invoices.UnpaidCount} st obetalda · {invoices.TotalUnpaidSek:N0} kr.";

        var insight = new ActionCenterInsight
        {
            Key = "unpaid-invoices",
            Audience = ActionCenterAudience.Customer,
            Category = "Fakturor",
            Status = ActionCenterStatus.Open,
            Title = title,
            Description = desc,
            Priority = overdue.Count > 0 ? ActionCenterPriority.High : ActionCenterPriority.Medium,
            DetectedAt = detectedAt,
            DueAt = overdue.Count > 0 ? detectedAt.AddDays(2) : null,
            LinkText = "Hantera fakturor",
            LinkUrl = "/Invoices/Index?tab=unpaid"
        };

        return new[] { insight };
    }
}

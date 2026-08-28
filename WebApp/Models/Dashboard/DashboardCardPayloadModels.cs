// Defines the feature-specific payloads rendered inside independently composed dashboard cards.
using WebApp.Models.ActionCenter;
using WebApp.Models.CustomerActivity;
using WebApp.Models.DocumentSigning;
using WebApp.Models.Orders;
using WebApp.ViewModels.Invoices;
using WebApp.ViewModels.NotifyMe;

namespace WebApp.Models.Dashboard;

public sealed class InvoiceSummaryCardViewModel
{
    public InvoiceListViewModel Invoices { get; init; } = new();
}

public sealed class OverdueInvoicesCardViewModel
{
    public InvoiceListViewModel Invoices { get; init; } = new();
}

public sealed class ModuleShortcutCardViewModel
{
    public int OpenCount { get; init; }
    public string StatusSummary { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public string ActionLabel { get; init; } = string.Empty;
    public string Controller { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
}

public sealed class CustomerActivityCardViewModel
{
    public CustomerActivityViewModel Activity { get; init; } = new();
}

public sealed class ActionCenterCardViewModel
{
    public ActionCenterViewModel ActionCenter { get; init; } = new();
}

public sealed class NotifyMeCardViewModel
{
    public NotifyMeOverviewVm Overview { get; init; } = new();
}

public sealed class DeliveryStatusCardViewModel
{
    public OrderDeliveryForecastViewModel Forecast { get; init; } = new();
}

public sealed class InventoryStatusCardViewModel
{
    public int TotalSignals { get; init; }
    public int HighPriorityCount { get; init; }
    public int WarningCount { get; init; }
    public IReadOnlyList<ActionCenterInsight> Signals { get; init; } = Array.Empty<ActionCenterInsight>();
    public string? StatusMessage { get; init; }
}

public sealed class PurchaseAcknowledgementCardViewModel
{
    public int TotalOrders { get; init; }
    public int AwaitingAcknowledgementCount { get; init; }
    public int OrderedCount { get; init; }
    public int OverdueCount { get; init; }
    public IReadOnlyList<PurchaseAcknowledgementOrderVm> RecentOrders { get; init; } = Array.Empty<PurchaseAcknowledgementOrderVm>();
}

public sealed class PurchaseAcknowledgementOrderVm
{
    public int? OrderNumber { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public int? OrderStatusId { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public DateTime? DeliveryDate { get; init; }
    public decimal OrderValue { get; init; }
    public string Currency { get; init; } = string.Empty;
    public bool IsOverdue { get; init; }
}

public sealed class DocumentSigningCardViewModel
{
    public bool IsConfigured { get; init; }
    public string? StatusMessage { get; init; }
    public int TotalSignings { get; init; }
    public int ActiveCount { get; init; }
    public int SignedCount { get; init; }
    public int NeedsAttentionCount { get; init; }
    public IReadOnlyList<DocumentSigningListItem> RecentSignings { get; init; } = Array.Empty<DocumentSigningListItem>();
}

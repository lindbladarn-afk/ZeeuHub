// Shared dashboard composition models used to assemble the member dashboard from card features.
using WebApp.ViewModels.Shared;

namespace WebApp.Models.Dashboard;

public static class DashboardCardIds
{
    public const string Revenue = "revenue";
    public const string RevenueSummary = "revenue-summary";
    public const string AverageOrderValue = "average-order-value";
    public const string RevenueTrend = "revenue-trend";
    public const string TopSellers = "top-sellers";
    public const string InvoiceSummary = "invoice-summary";
    public const string OverdueInvoices = "overdue-invoices";
    public const string BankReconciliation = "bank-reconciliation";
    public const string PurchaseApproval = "purchase-approval";
    public const string CustomerActivity = "customer-activity";
    public const string ActionCenter = "action-center";
    public const string NotifyMe = "notifyme";
    public const string DeliveryStatus = "delivery-status";
    public const string InventoryStatus = "inventory-status";
    public const string PurchaseAcknowledgement = "purchase-acknowledgement";
    public const string DocumentSigning = "document-signing";
}

public static class DashboardWidgetCategories
{
    public const string Overview = "overview";
    public const string Finance = "finance";
    public const string Sales = "sales";
    public const string Orders = "orders";
    public const string Operations = "operations";
}

public enum DashboardWidgetSize
{
    Compact = 0,
    Wide = 1,
    Full = 2
}

public enum DashboardCardState
{
    Ready = 0,
    Loading = 1,
    Empty = 2,
    Error = 3
}

public sealed class DashboardCardDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = DashboardWidgetCategories.Overview;
    public int SortOrder { get; init; }
    public string RenderViewName { get; init; } = string.Empty;
    public string ColumnCssClass { get; init; } = "col-12";
    public DashboardWidgetSize DefaultSize { get; init; } = DashboardWidgetSize.Compact;
    public IReadOnlyList<DashboardWidgetSize> SupportedSizes { get; init; } =
        [DashboardWidgetSize.Compact, DashboardWidgetSize.Wide, DashboardWidgetSize.Full];
    public bool RequiresDataAccess { get; init; }
    public IReadOnlyList<Guid> PermissionIds { get; init; } = Array.Empty<Guid>();
    public bool Enabled { get; init; } = true;
}

public sealed class DashboardWidgetLayout
{
    public string WidgetId { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public DashboardWidgetSize Size { get; init; } = DashboardWidgetSize.Compact;
    public bool IsVisible { get; init; } = true;
}

public sealed class DashboardCardViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public string RenderViewName { get; init; } = string.Empty;
    public string ColumnCssClass { get; init; } = "col-12";
    public DashboardWidgetSize Size { get; init; } = DashboardWidgetSize.Compact;
    public IReadOnlyList<DashboardWidgetSize> SupportedSizes { get; init; } =
        [DashboardWidgetSize.Compact, DashboardWidgetSize.Wide, DashboardWidgetSize.Full];
    public string? LazyLoadUrl { get; init; }
    public DashboardCardState State { get; init; } = DashboardCardState.Ready;
    public string? StateTitle { get; init; }
    public string? StateMessage { get; init; }
    public DateTime? LastUpdatedAtUtc { get; init; }
    public object? Data { get; init; }
}

public sealed class MemberDashboardPageViewModel
{
    public string ActiveCompanyName { get; init; } = string.Empty;
    public int? ActiveCompanyCode { get; init; }
    public bool HasDataAccess { get; init; } = true;
    public string? DataAccessWarning { get; init; }
    public RevenueAnalysisContext RevenueAnalysis { get; init; } = new();
    public ModuleStateViewModel? RuntimeState { get; init; }
    public IReadOnlyList<DashboardCardViewModel> Cards { get; init; } = Array.Empty<DashboardCardViewModel>();
    public IReadOnlyList<DashboardCardDefinition> AvailableCards { get; init; } = Array.Empty<DashboardCardDefinition>();
}

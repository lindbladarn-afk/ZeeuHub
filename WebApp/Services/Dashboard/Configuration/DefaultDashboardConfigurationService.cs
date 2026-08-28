// Defines the dashboard catalog, size constraints, defaults, and company permission filtering.
using Entities.Application;
using WebApp.Models.Dashboard;
using WebApp.Services.Application;

namespace WebApp.Services.Dashboard;

public sealed class DefaultDashboardConfigurationService : IDashboardConfigurationService
{
    private static readonly IReadOnlyList<DashboardCardDefinition> Catalog =
    [
        Define(DashboardCardIds.ActionCenter, "Action Center", "Det viktigaste som behöver åtgärdas nu.", DashboardWidgetCategories.Overview, 10, "_ActionCenterCard", DashboardWidgetSize.Full, [DashboardWidgetSize.Wide, DashboardWidgetSize.Full]),
        Define(DashboardCardIds.Revenue, "Omsättning – senaste 12 månader", "Totalt omsättningsvärde för de senaste 12 månaderna.", DashboardWidgetCategories.Sales, 20, "_RevenueMetricCard", DashboardWidgetSize.Compact, [DashboardWidgetSize.Compact, DashboardWidgetSize.Wide], requiresDataAccess: true),
        Define(DashboardCardIds.AverageOrderValue, "Snittordervärde – senaste 30 dagar", "Genomsnittligt ordervärde för de senaste 30 dagarna.", DashboardWidgetCategories.Sales, 22, "_RevenueMetricCard", DashboardWidgetSize.Compact, [DashboardWidgetSize.Compact, DashboardWidgetSize.Wide], requiresDataAccess: true),
        Define(DashboardCardIds.RevenueTrend, "Omsättning – utveckling över tid", "Omsättning per vecka, månad eller kvartal i ett separat diagram.", DashboardWidgetCategories.Sales, 23, "_RevenueMetricCard", DashboardWidgetSize.Wide, [DashboardWidgetSize.Wide, DashboardWidgetSize.Full], requiresDataAccess: true),
        Define(DashboardCardIds.TopSellers, "Toppsäljande produkter – separat lista", "Produkter med högst omsättning, helt fristående från omsättningskorten.", DashboardWidgetCategories.Sales, 24, "_RevenueMetricCard", DashboardWidgetSize.Wide, [DashboardWidgetSize.Compact, DashboardWidgetSize.Wide, DashboardWidgetSize.Full], requiresDataAccess: true),
        Define(DashboardCardIds.InvoiceSummary, "Fakturor", "Översikt över betalda och obetalda fakturor.", DashboardWidgetCategories.Finance, 30, "_InvoiceSummaryCard", DashboardWidgetSize.Compact, [DashboardWidgetSize.Compact, DashboardWidgetSize.Wide], [PortalModuleIds.InvoicesSubModule], requiresDataAccess: true),
        Define(DashboardCardIds.OverdueInvoices, "Förfallna kundfakturor", "Förfallna fakturor som behöver följas upp.", DashboardWidgetCategories.Finance, 31, "_OverdueInvoicesCard", DashboardWidgetSize.Compact, [DashboardWidgetSize.Compact, DashboardWidgetSize.Wide], [PortalModuleIds.InvoicesSubModule], requiresDataAccess: true),
        Define(DashboardCardIds.BankReconciliation, "Bankavstämning", "Öppna arbetsytan för matchning och avstämning av banktransaktioner.", DashboardWidgetCategories.Finance, 32, "_ModuleShortcutCard", DashboardWidgetSize.Compact, [DashboardWidgetSize.Compact, DashboardWidgetSize.Wide], [PortalModuleIds.BankReconciliationModule, PortalModuleIds.BankReconciliationSubModule], requiresDataAccess: true),
        Define(DashboardCardIds.PurchaseApproval, "Inköpsattest", "Öppna kön för inköpsorder som väntar på attest.", DashboardWidgetCategories.Orders, 33, "_ModuleShortcutCard", DashboardWidgetSize.Compact, [DashboardWidgetSize.Compact, DashboardWidgetSize.Wide], requiresDataAccess: true),
        Define(DashboardCardIds.CustomerActivity, "Kundaktivitet", "Senaste kundhändelserna i valt bolag.", DashboardWidgetCategories.Sales, 40, "_CustomerActivityCard", DashboardWidgetSize.Compact, [DashboardWidgetSize.Compact, DashboardWidgetSize.Wide], requiresDataAccess: true),
        Define(DashboardCardIds.NotifyMe, "NotifyMe", "Aktiva notifieringar och senaste körningar.", DashboardWidgetCategories.Operations, 50, "_NotifyMeCard", DashboardWidgetSize.Compact, [DashboardWidgetSize.Compact, DashboardWidgetSize.Wide]),
        Define(DashboardCardIds.DeliveryStatus, "Leveransstatus", "Kommande leveranser och orderbacklog.", DashboardWidgetCategories.Orders, 60, "_DeliveryStatusCard", DashboardWidgetSize.Wide, [DashboardWidgetSize.Wide, DashboardWidgetSize.Full], [PortalModuleIds.OrdersSubModule], requiresDataAccess: true),
        Define(DashboardCardIds.InventoryStatus, "Lagerstatus", "Lagerrelaterade signaler från Action Center.", DashboardWidgetCategories.Orders, 70, "_InventoryStatusCard", DashboardWidgetSize.Compact, [DashboardWidgetSize.Compact, DashboardWidgetSize.Wide], requiresDataAccess: true),
        Define(DashboardCardIds.PurchaseAcknowledgement, "Ordererkännande", "Inköpsorder som väntar på bekräftelse eller leverans.", DashboardWidgetCategories.Orders, 80, "_PurchaseAcknowledgementCard", DashboardWidgetSize.Compact, [DashboardWidgetSize.Compact, DashboardWidgetSize.Wide], requiresDataAccess: true),
        Define(DashboardCardIds.DocumentSigning, "Dokumentsignering", "Aktiva och nyligen uppdaterade signeringsärenden.", DashboardWidgetCategories.Operations, 90, "_DocumentSigningCard", DashboardWidgetSize.Compact, [DashboardWidgetSize.Compact, DashboardWidgetSize.Wide, DashboardWidgetSize.Full], [PortalModuleIds.DocumentSigningSubModule], requiresDataAccess: true)
    ];

    private readonly ICompanyPermissionGuard _permissionGuard;

    public DefaultDashboardConfigurationService(ICompanyPermissionGuard permissionGuard)
    {
        _permissionGuard = permissionGuard;
    }

    public async Task<IReadOnlyList<DashboardCardDefinition>> GetAvailableCardsAsync(
        UserSession? user,
        CancellationToken cancellationToken = default)
    {
        if (user?.CompanyId is not Guid companyId || companyId == Guid.Empty)
        {
            return Catalog
                .Where(card => card.PermissionIds.Count == 0 && !card.RequiresDataAccess)
                .ToList();
        }

        var permissionIds = Catalog.SelectMany(card => card.PermissionIds).Distinct().ToArray();
        var permissionResults = await Task.WhenAll(permissionIds.Select(async permissionId =>
            new
            {
                PermissionId = permissionId,
                HasAccess = await _permissionGuard.HasAccessAsync(companyId, permissionId, cancellationToken)
            }));
        var accessByPermission = permissionResults.ToDictionary(result => result.PermissionId, result => result.HasAccess);

        return Catalog
            .Where(card => card.PermissionIds.Count == 0
                || card.PermissionIds.Any(id => accessByPermission.GetValueOrDefault(id)))
            .ToList();
    }

    public IReadOnlyList<DashboardWidgetLayout> GetDefaultLayout(UserSession? user)
        =>
        [
            CreateDefault(DashboardCardIds.ActionCenter, 10, DashboardWidgetSize.Full),
            CreateDefault(DashboardCardIds.Revenue, 20, DashboardWidgetSize.Compact),
            CreateDefault(DashboardCardIds.AverageOrderValue, 30, DashboardWidgetSize.Compact),
            CreateDefault(DashboardCardIds.RevenueTrend, 40, DashboardWidgetSize.Wide),
            CreateDefault(DashboardCardIds.TopSellers, 50, DashboardWidgetSize.Wide),
            CreateDefault(DashboardCardIds.InvoiceSummary, 60, DashboardWidgetSize.Compact),
            CreateDefault(DashboardCardIds.DeliveryStatus, 70, DashboardWidgetSize.Wide),
            CreateDefault(DashboardCardIds.NotifyMe, 80, DashboardWidgetSize.Compact)
        ];

    private static DashboardCardDefinition Define(
        string id,
        string title,
        string description,
        string category,
        int sortOrder,
        string viewName,
        DashboardWidgetSize defaultSize,
        IReadOnlyList<DashboardWidgetSize> supportedSizes,
        IReadOnlyList<Guid>? permissionIds = null,
        bool requiresDataAccess = false)
        => new()
        {
            Id = id,
            Title = title,
            Description = description,
            Category = category,
            SortOrder = sortOrder,
            RenderViewName = $"Dashboard/Cards/{viewName}",
            DefaultSize = defaultSize,
            SupportedSizes = supportedSizes,
            PermissionIds = permissionIds ?? [],
            RequiresDataAccess = requiresDataAccess
        };

    private static DashboardWidgetLayout CreateDefault(
        string widgetId,
        int sortOrder,
        DashboardWidgetSize size)
        => new()
        {
            WidgetId = widgetId,
            SortOrder = sortOrder,
            Size = size,
            IsVisible = true
        };
}

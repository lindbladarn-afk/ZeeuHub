// Builds purchase acknowledgement metrics and the most recent supplier orders.
using Entities.Purchase;
using WebApp.Models.Dashboard;
using WebApp.Services.Dashboard.Demo;
using WebApp.Services.Purchase.Orders;

namespace WebApp.Services.Dashboard;

public sealed class PurchaseAcknowledgementDashboardCardProvider : IDashboardCardProvider
{
    private readonly IPurchaseOrderService _purchaseOrderService;
    private readonly IDashboardDemoDataService _demoData;
    private readonly DashboardCardViewModelFactory _cards;
    private readonly ILogger<PurchaseAcknowledgementDashboardCardProvider> _logger;

    public PurchaseAcknowledgementDashboardCardProvider(
        IPurchaseOrderService purchaseOrderService,
        IDashboardDemoDataService demoData,
        DashboardCardViewModelFactory cards,
        ILogger<PurchaseAcknowledgementDashboardCardProvider> logger)
    {
        _purchaseOrderService = purchaseOrderService;
        _demoData = demoData;
        _cards = cards;
        _logger = logger;
    }

    public IReadOnlyCollection<string> CardIds => [DashboardCardIds.PurchaseAcknowledgement];

    public async Task<DashboardCardViewModel?> BuildAsync(
        DashboardCardDefinition definition,
        DashboardCardBuildContext context,
        CancellationToken cancellationToken)
    {
        if (context.UseDemoData)
        {
            return _cards.Create(definition, _demoData.BuildPurchaseAcknowledgement());
        }

        List<PurchaseAcknowledgementOrderVm> orders;
        try
        {
            var items = await _purchaseOrderService.GetMyPurchaseOrdersAsync(cancellationToken);
            orders = items
                .OrderByDescending(item => item.DeliveryDate ?? item.RegisteredDate ?? DateTime.MinValue)
                .ThenByDescending(item => item.OrderNumber ?? 0)
                .Select(MapOrder)
                .ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build purchase acknowledgement dashboard card.");
            return _cards.Create(
                definition,
                new PurchaseAcknowledgementCardViewModel(),
                DashboardCardState.Error,
                "Ordererkännanden kunde inte laddas",
                "Försök igen för att hämta aktuella inköpsorder.");
        }

        var data = new PurchaseAcknowledgementCardViewModel
        {
            TotalOrders = orders.Count,
            AwaitingAcknowledgementCount = orders.Count(order => order.OrderStatusId is 10 or 20),
            OrderedCount = orders.Count(order => order.OrderStatusId is 20 or 40),
            OverdueCount = orders.Count(order => order.IsOverdue),
            RecentOrders = orders.Take(5).ToList()
        };

        return orders.Count == 0
            ? _cards.Create(
                definition,
                data,
                DashboardCardState.Empty,
                "Inga inköpsorder att visa",
                "Det finns inga aktuella ordererkännanden för dig just nu.")
            : _cards.Create(definition, data);
    }

    private static PurchaseAcknowledgementOrderVm MapOrder(IPurchaseOrderVM order)
    {
        var statusLabel = order.OrderStatusId switch
        {
            10 => "Godkänd",
            20 => "Beställd",
            40 => "Delvis levererad",
            70 => "Levererad",
            90 => "Makulerad",
            _ => "Okänd"
        };

        return new PurchaseAcknowledgementOrderVm
        {
            OrderNumber = order.OrderNumber,
            SupplierName = order.SupplierName,
            OrderStatusId = order.OrderStatusId,
            StatusLabel = statusLabel,
            DeliveryDate = order.DeliveryDate,
            OrderValue = order.OrderValue,
            Currency = order.Currency,
            IsOverdue = order.DeliveryDate.HasValue
                && order.DeliveryDate.Value.Date < DateTime.Today
                && (order.OrderStatusId ?? 0) < 70
        };
    }
}

// Builds delivery forecast cards for the selected company and planning horizon.
using WebApp.Models.Dashboard;
using WebApp.Models.Orders;
using WebApp.Services.Dashboard.Demo;
using WebApp.Services.Orders;

namespace WebApp.Services.Dashboard;

public sealed class DeliveryStatusDashboardCardProvider : IDashboardCardProvider
{
    private readonly IOrdersService _ordersService;
    private readonly IDashboardDemoDataService _demoData;
    private readonly DashboardCardViewModelFactory _cards;
    private readonly ILogger<DeliveryStatusDashboardCardProvider> _logger;

    public DeliveryStatusDashboardCardProvider(
        IOrdersService ordersService,
        IDashboardDemoDataService demoData,
        DashboardCardViewModelFactory cards,
        ILogger<DeliveryStatusDashboardCardProvider> logger)
    {
        _ordersService = ordersService;
        _demoData = demoData;
        _cards = cards;
        _logger = logger;
    }

    public IReadOnlyCollection<string> CardIds => [DashboardCardIds.DeliveryStatus];

    public async Task<DashboardCardViewModel?> BuildAsync(
        DashboardCardDefinition definition,
        DashboardCardBuildContext context,
        CancellationToken cancellationToken)
    {
        if (context.UseDemoData)
        {
            return _cards.Create(
                definition,
                new DeliveryStatusCardViewModel { Forecast = _demoData.BuildDeliveryForecast() });
        }

        if (context.RuntimeContext is null)
        {
            return Error(definition, "Kontrollera anslutningen för valt bolag och försök igen.");
        }

        try
        {
            var forecast = await _ordersService.GetDeliveryForecastAsync(
                context.RuntimeContext.ConnectionString,
                new GetDeliveryForecastQuery
                {
                    CompanyCode = context.RuntimeContext.CompanyCode,
                    MonthsAhead = 6,
                    Page = 1,
                    PageSize = 6
                });
            var data = new DeliveryStatusCardViewModel { Forecast = forecast };

            return forecast.Timeline.Count == 0 && forecast.UpcomingOrders.Count == 0
                ? _cards.Create(
                    definition,
                    data,
                    DashboardCardState.Empty,
                    "Inga kommande leveranser",
                    "Det finns inga planerade leveranser i den valda perioden.")
                : _cards.Create(definition, data);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build delivery status dashboard card.");
            return Error(definition, "Kontrollera anslutningen och försök igen.");
        }
    }

    private DashboardCardViewModel Error(DashboardCardDefinition definition, string message)
        => _cards.Create(
            definition,
            new DeliveryStatusCardViewModel(),
            DashboardCardState.Error,
            "Leveransstatus kunde inte laddas",
            message);
}

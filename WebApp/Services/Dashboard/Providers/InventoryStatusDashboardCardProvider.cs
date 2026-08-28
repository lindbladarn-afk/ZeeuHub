// Builds inventory alerts from stock-related Action Center insights.
using WebApp.Models.ActionCenter;
using WebApp.Models.Dashboard;
using WebApp.Services.ActionCenter;
using WebApp.Services.Dashboard.Demo;

namespace WebApp.Services.Dashboard;

public sealed class InventoryStatusDashboardCardProvider : IDashboardCardProvider
{
    private readonly IActionCenterService _actionCenterService;
    private readonly IDashboardDemoDataService _demoData;
    private readonly DashboardCardViewModelFactory _cards;
    private readonly ILogger<InventoryStatusDashboardCardProvider> _logger;

    public InventoryStatusDashboardCardProvider(
        IActionCenterService actionCenterService,
        IDashboardDemoDataService demoData,
        DashboardCardViewModelFactory cards,
        ILogger<InventoryStatusDashboardCardProvider> logger)
    {
        _actionCenterService = actionCenterService;
        _demoData = demoData;
        _cards = cards;
        _logger = logger;
    }

    public IReadOnlyCollection<string> CardIds => [DashboardCardIds.InventoryStatus];

    public async Task<DashboardCardViewModel?> BuildAsync(
        DashboardCardDefinition definition,
        DashboardCardBuildContext context,
        CancellationToken cancellationToken)
    {
        if (context.UseDemoData)
        {
            return _cards.Create(definition, _demoData.BuildInventoryStatus());
        }

        if (context.User is null)
        {
            return Error(definition, "En aktiv användarsession krävs. Ladda om sidan och försök igen.");
        }

        List<ActionCenterInsight> signals;
        try
        {
            var actionCenter = await _actionCenterService.GetInsightsAsync(context.User, take: 25, cancellationToken);
            signals = actionCenter.Insights
                .Where(insight =>
                    string.Equals(insight.Category, "Lager", StringComparison.OrdinalIgnoreCase)
                    || insight.Key.Contains("stock", StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build inventory status dashboard card.");
            return Error(definition, "Försök igen för att hämta lagerrelaterade signaler.");
        }

        var data = new InventoryStatusCardViewModel
        {
            TotalSignals = signals.Count,
            HighPriorityCount = signals.Count(signal => signal.Priority == ActionCenterPriority.High),
            WarningCount = signals.Count(signal =>
                signal.Priority is ActionCenterPriority.Medium or ActionCenterPriority.High),
            Signals = signals
        };

        return signals.Count == 0
            ? _cards.Create(
                definition,
                data,
                DashboardCardState.Empty,
                "Inga lagersignaler",
                "Det finns inget lagerrelaterat som behöver din uppmärksamhet just nu.")
            : _cards.Create(definition, data);
    }

    private DashboardCardViewModel Error(DashboardCardDefinition definition, string message)
        => _cards.Create(
            definition,
            new InventoryStatusCardViewModel(),
            DashboardCardState.Error,
            "Lagerstatus kunde inte laddas",
            message);
}

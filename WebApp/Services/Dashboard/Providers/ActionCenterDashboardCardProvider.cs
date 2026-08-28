// Builds the lazy Action Center card and limits the dashboard to its highest-priority insights.
using WebApp.Models.ActionCenter;
using WebApp.Models.Dashboard;
using WebApp.Services.ActionCenter;
using WebApp.Services.Dashboard.Demo;

namespace WebApp.Services.Dashboard;

public sealed class ActionCenterDashboardCardProvider : IDashboardCardProvider
{
    private readonly IActionCenterService _actionCenterService;
    private readonly IDashboardDemoDataService _demoData;
    private readonly DashboardCardViewModelFactory _cards;
    private readonly ILogger<ActionCenterDashboardCardProvider> _logger;

    public ActionCenterDashboardCardProvider(
        IActionCenterService actionCenterService,
        IDashboardDemoDataService demoData,
        DashboardCardViewModelFactory cards,
        ILogger<ActionCenterDashboardCardProvider> logger)
    {
        _actionCenterService = actionCenterService;
        _demoData = demoData;
        _cards = cards;
        _logger = logger;
    }

    public IReadOnlyCollection<string> CardIds => [DashboardCardIds.ActionCenter];

    public async Task<DashboardCardViewModel?> BuildAsync(
        DashboardCardDefinition definition,
        DashboardCardBuildContext context,
        CancellationToken cancellationToken)
    {
        if (!context.IsSingleCardRequest && !context.UseDemoData)
        {
            return _cards.Loading(
                definition,
                new ActionCenterCardViewModel(),
                context.GetRefreshUrl(definition.Id));
        }

        if (context.UseDemoData)
        {
            return CreateResult(definition, Limit(_demoData.BuildActionCenter(), take: 3));
        }

        if (context.User is null)
        {
            return _cards.Create(
                definition,
                new ActionCenterCardViewModel(),
                DashboardCardState.Error,
                "Action Center kunde inte laddas",
                "En aktiv användarsession krävs. Ladda om sidan och försök igen.");
        }

        try
        {
            var actionCenter = await _actionCenterService.GetInsightsAsync(context.User, take: 3, cancellationToken);
            return CreateResult(definition, actionCenter);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build Action Center dashboard card.");
            return _cards.Create(
                definition,
                new ActionCenterCardViewModel(),
                DashboardCardState.Error,
                "Action Center kunde inte laddas",
                "Försök igen för att hämta de senaste insikterna.");
        }
    }

    private DashboardCardViewModel CreateResult(DashboardCardDefinition definition, ActionCenterViewModel actionCenter)
        => actionCenter.Insights.Count == 0
            ? _cards.Create(
                definition,
                new ActionCenterCardViewModel { ActionCenter = actionCenter },
                DashboardCardState.Empty,
                "Inga aktuella åtgärder",
                "Det finns inget som behöver din uppmärksamhet just nu.")
            : _cards.Create(
                definition,
                new ActionCenterCardViewModel { ActionCenter = actionCenter });

    private static ActionCenterViewModel Limit(ActionCenterViewModel actionCenter, int take)
        => new()
        {
            TotalCount = actionCenter.TotalCount,
            Audience = actionCenter.Audience,
            IsDegraded = actionCenter.IsDegraded,
            AvailabilityBanner = actionCenter.AvailabilityBanner,
            Insights = actionCenter.Insights
                .OrderByDescending(insight => insight.Priority)
                .ThenByDescending(insight => insight.DetectedAt)
                .Take(take)
                .ToList(),
            History = actionCenter.History,
            ProviderFailures = actionCenter.ProviderFailures
        };
}

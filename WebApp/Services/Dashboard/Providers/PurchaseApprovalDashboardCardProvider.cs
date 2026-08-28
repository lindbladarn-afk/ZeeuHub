// Builds the purchase approval shortcut from Action Center approval insights.
using WebApp.Models.ActionCenter;
using WebApp.Models.Dashboard;
using WebApp.Services.ActionCenter;

namespace WebApp.Services.Dashboard;

public sealed class PurchaseApprovalDashboardCardProvider : IDashboardCardProvider
{
    private readonly IActionCenterService _actionCenterService;
    private readonly DashboardCardViewModelFactory _cards;
    private readonly ILogger<PurchaseApprovalDashboardCardProvider> _logger;

    public PurchaseApprovalDashboardCardProvider(
        IActionCenterService actionCenterService,
        DashboardCardViewModelFactory cards,
        ILogger<PurchaseApprovalDashboardCardProvider> logger)
    {
        _actionCenterService = actionCenterService;
        _cards = cards;
        _logger = logger;
    }

    public IReadOnlyCollection<string> CardIds => [DashboardCardIds.PurchaseApproval];

    public async Task<DashboardCardViewModel?> BuildAsync(
        DashboardCardDefinition definition,
        DashboardCardBuildContext context,
        CancellationToken cancellationToken)
    {
        var actionCenter = new ActionCenterViewModel();
        try
        {
            if (context.User is not null)
            {
                actionCenter = await _actionCenterService.GetInsightsAsync(context.User, 50, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build purchase approval dashboard card.");
            return _cards.Create(
                definition,
                BuildData([]),
                DashboardCardState.Error,
                "Inköpsattest kunde inte laddas",
                "Försök igen för att hämta aktuella attestärenden.");
        }

        var approvals = actionCenter.Insights
            .Where(insight => string.Equals(insight.Category, "Attest", StringComparison.OrdinalIgnoreCase))
            .OrderBy(insight => insight.DetectedAt)
            .ToList();
        var data = BuildData(approvals);

        return approvals.Count == 0
            ? _cards.Create(
                definition,
                data,
                DashboardCardState.Empty,
                "Inga väntande inköpsattester",
                "Det finns inget som behöver attesteras just nu.")
            : _cards.Create(definition, data);
    }

    private static ModuleShortcutCardViewModel BuildData(IReadOnlyList<ActionCenterInsight> approvals)
    {
        var oldest = approvals.FirstOrDefault();
        return new ModuleShortcutCardViewModel
        {
            OpenCount = approvals.Count,
            StatusSummary = approvals.Count == 1
                ? "1 inköpsattest väntar"
                : $"{approvals.Count} inköpsattester väntar",
            Detail = oldest is null
                ? "Inga inköpsattester väntar just nu."
                : $"Äldst: {oldest.DetectedAt:yyyy-MM-dd}",
            ActionLabel = "Öppna inköpsattest",
            Controller = "WebApproval",
            Action = "PurchaseApproval"
        };
    }
}

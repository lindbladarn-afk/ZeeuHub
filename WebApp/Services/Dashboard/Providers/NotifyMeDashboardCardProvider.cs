// Builds the NotifyMe overview with consistent unavailable, empty, and error states.
using WebApp.Models.Dashboard;
using WebApp.Services.Dashboard.Demo;
using WebApp.Services.NotifyMe;
using WebApp.ViewModels.NotifyMe;

namespace WebApp.Services.Dashboard;

public sealed class NotifyMeDashboardCardProvider : IDashboardCardProvider
{
    private readonly INotifyMeService _notifyMeService;
    private readonly IDashboardDemoDataService _demoData;
    private readonly DashboardCardViewModelFactory _cards;
    private readonly ILogger<NotifyMeDashboardCardProvider> _logger;

    public NotifyMeDashboardCardProvider(
        INotifyMeService notifyMeService,
        IDashboardDemoDataService demoData,
        DashboardCardViewModelFactory cards,
        ILogger<NotifyMeDashboardCardProvider> logger)
    {
        _notifyMeService = notifyMeService;
        _demoData = demoData;
        _cards = cards;
        _logger = logger;
    }

    public IReadOnlyCollection<string> CardIds => [DashboardCardIds.NotifyMe];

    public async Task<DashboardCardViewModel?> BuildAsync(
        DashboardCardDefinition definition,
        DashboardCardBuildContext context,
        CancellationToken cancellationToken)
    {
        if (context.UseDemoData)
        {
            return _cards.Create(
                definition,
                new NotifyMeCardViewModel { Overview = _demoData.BuildNotifyMeOverview() });
        }

        NotifyMeOverviewVm overview;
        try
        {
            overview = await _notifyMeService.GetOverviewAsync(
                context.RuntimeContext?.ConnectionString,
                context.RuntimeContext?.CompanyCode,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build NotifyMe dashboard card.");
            return _cards.Create(
                definition,
                new NotifyMeCardViewModel(),
                DashboardCardState.Error,
                "NotifyMe kunde inte laddas",
                "Försök igen för att hämta notifieringar och senaste körningar.");
        }

        var data = new NotifyMeCardViewModel { Overview = overview };
        return !overview.IsInstalled
            ? _cards.Create(
                definition,
                data,
                DashboardCardState.Empty,
                "NotifyMe är inte aktiverat",
                overview.StatusMessage ?? "NotifyMe finns inte i det valda bolaget.")
            : _cards.Create(definition, data);
    }
}

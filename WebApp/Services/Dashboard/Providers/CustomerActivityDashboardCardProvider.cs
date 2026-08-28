// Builds the lazy customer activity card from the active Jeeves company context.
using WebApp.Models.CustomerActivity;
using WebApp.Models.Dashboard;
using WebApp.Services.CustomerActivity;

namespace WebApp.Services.Dashboard;

public sealed class CustomerActivityDashboardCardProvider : IDashboardCardProvider
{
    private readonly ICustomerActivityService _customerActivityService;
    private readonly DashboardCardViewModelFactory _cards;
    private readonly ILogger<CustomerActivityDashboardCardProvider> _logger;

    public CustomerActivityDashboardCardProvider(
        ICustomerActivityService customerActivityService,
        DashboardCardViewModelFactory cards,
        ILogger<CustomerActivityDashboardCardProvider> logger)
    {
        _customerActivityService = customerActivityService;
        _cards = cards;
        _logger = logger;
    }

    public IReadOnlyCollection<string> CardIds => [DashboardCardIds.CustomerActivity];

    public async Task<DashboardCardViewModel?> BuildAsync(
        DashboardCardDefinition definition,
        DashboardCardBuildContext context,
        CancellationToken cancellationToken)
    {
        if (!context.IsSingleCardRequest)
        {
            return _cards.Loading(
                definition,
                new CustomerActivityCardViewModel(),
                context.GetRefreshUrl(definition.Id));
        }

        if (context.RuntimeContext is null)
        {
            return Error(definition, "Kontrollera anslutningen för valt bolag och försök igen.");
        }

        try
        {
            var activity = await _customerActivityService.GetRecentAsync(
                context.RuntimeContext.ConnectionString,
                context.RuntimeContext.CompanyCode,
                take: 5);

            return activity.Items.Count == 0
                ? _cards.Create(
                    definition,
                    new CustomerActivityCardViewModel { Activity = activity },
                    DashboardCardState.Empty,
                    "Ingen kundaktivitet ännu",
                    "De senaste kundhändelserna visas här när de finns tillgängliga.")
                : _cards.Create(
                    definition,
                    new CustomerActivityCardViewModel { Activity = activity });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build customer activity dashboard card.");
            return Error(definition, "Försök igen för att hämta de senaste kundhändelserna.");
        }
    }

    private DashboardCardViewModel Error(DashboardCardDefinition definition, string message)
        => _cards.Create(
            definition,
            new CustomerActivityCardViewModel
            {
                Activity = new CustomerActivityViewModel()
            },
            DashboardCardState.Error,
            "Kundaktivitet kunde inte laddas",
            message);
}

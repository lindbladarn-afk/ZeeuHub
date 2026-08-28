// Builds revenue, invoice, and finance shortcut cards from request-cached source data.
using WebApp.Models.Dashboard;

namespace WebApp.Services.Dashboard;

public sealed class RevenueDashboardCardProvider : IDashboardCardProvider
{
    private static readonly string[] SupportedCardIds =
    [
        DashboardCardIds.Revenue,
        DashboardCardIds.RevenueSummary,
        DashboardCardIds.AverageOrderValue,
        DashboardCardIds.RevenueTrend,
        DashboardCardIds.TopSellers,
        DashboardCardIds.InvoiceSummary,
        DashboardCardIds.OverdueInvoices,
        DashboardCardIds.BankReconciliation
    ];

    private readonly DashboardCardViewModelFactory _cards;

    public RevenueDashboardCardProvider(DashboardCardViewModelFactory cards)
    {
        _cards = cards;
    }

    public IReadOnlyCollection<string> CardIds => SupportedCardIds;

    public async Task<DashboardCardViewModel?> BuildAsync(
        DashboardCardDefinition definition,
        DashboardCardBuildContext context,
        CancellationToken cancellationToken)
    {
        if (definition.Id == DashboardCardIds.BankReconciliation)
        {
            return _cards.Create(
                definition,
                new ModuleShortcutCardViewModel
                {
                    StatusSummary = "Matcha och följ upp banktransaktioner",
                    Detail = "Öppna arbetsytan för att se importerade filer och aktuella avstämningsärenden.",
                    ActionLabel = "Öppna bankavstämning",
                    Controller = "BankReconciliation",
                    Action = "BankReconciliation"
                });
        }

        if (definition.Id is DashboardCardIds.InvoiceSummary or DashboardCardIds.OverdueInvoices)
        {
            var result = await context.Data.GetInvoicesAsync(cancellationToken);
            object data = definition.Id == DashboardCardIds.InvoiceSummary
                ? new InvoiceSummaryCardViewModel { Invoices = result.Value }
                : new OverdueInvoicesCardViewModel { Invoices = result.Value };

            if (result.Failed)
            {
                return _cards.Create(
                    definition,
                    data,
                    DashboardCardState.Error,
                    "Fakturor kunde inte laddas",
                    "Försök igen för att hämta aktuell fakturastatus.");
            }

            if (definition.Id == DashboardCardIds.InvoiceSummary
                && result.Value.UnpaidCount == 0
                && result.Value.PaidCount == 0)
            {
                return _cards.Create(
                    definition,
                    data,
                    DashboardCardState.Empty,
                    "Inga fakturor att visa",
                    "Det finns inga betalda eller obetalda fakturor i den aktuella perioden.");
            }

            if (definition.Id == DashboardCardIds.OverdueInvoices && result.Value.OverdueCount == 0)
            {
                return _cards.Create(
                    definition,
                    data,
                    DashboardCardState.Empty,
                    "Inga förfallna fakturor",
                    "Det finns inga förfallna kundfakturor som behöver följas upp.");
            }

            return _cards.Create(definition, data);
        }

        var revenueResult = await context.Data.GetRevenueAsync(cancellationToken);
        if (revenueResult.Failed)
        {
            return _cards.Create(
                definition,
                revenueResult.Value,
                DashboardCardState.Error,
                "Omsättningsdata kunde inte laddas",
                "Försök igen för att hämta uppdaterade försäljningssiffror.");
        }

        var emptyState = ResolveEmptyState(definition.Id, revenueResult.Value);
        return emptyState is null
            ? _cards.Create(definition, revenueResult.Value)
            : _cards.Create(
                definition,
                revenueResult.Value,
                DashboardCardState.Empty,
                emptyState.Value.Title,
                emptyState.Value.Message);
    }

    private static (string Title, string Message)? ResolveEmptyState(string cardId, RevenueDataModel revenue)
        => cardId switch
        {
            DashboardCardIds.Revenue or DashboardCardIds.RevenueSummary
                when revenue.AnnualRunRateDetails.PeriodStart is null
                => ("Ingen omsättning att visa", "Det finns ingen omsättningsdata i den aktuella perioden."),
            DashboardCardIds.AverageOrderValue
                when revenue.Kpi.OrdersCountPeriod <= 0
                => ("Inga order i perioden", "Snittordervärdet visas när det finns order under de senaste 30 dagarna."),
            DashboardCardIds.RevenueTrend
                when revenue.Week.Labels.Count == 0
                => ("Ingen omsättningsutveckling", "Grafen visas när det finns omsättningsdata för den valda perioden."),
            DashboardCardIds.TopSellers
                when revenue.TopSellers.Count == 0
                => ("Inga toppsäljande produkter", "Produktlistan visas när det finns försäljning i den aktuella perioden."),
            _ => null
        };
}

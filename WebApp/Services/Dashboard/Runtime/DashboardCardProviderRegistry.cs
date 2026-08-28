// Resolves card builders by stable card id and rejects ambiguous registrations.
using WebApp.Models.Dashboard;

namespace WebApp.Services.Dashboard;

public sealed class DashboardCardProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IDashboardCardProvider> _providers;

    public DashboardCardProviderRegistry(IEnumerable<IDashboardCardProvider> providers)
    {
        var providersByCardId = new Dictionary<string, IDashboardCardProvider>(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            foreach (var cardId in provider.CardIds)
            {
                if (string.IsNullOrWhiteSpace(cardId) || !providersByCardId.TryAdd(cardId, provider))
                {
                    throw new InvalidOperationException($"Dashboardkortet '{cardId}' har ingen unik provider.");
                }
            }
        }

        _providers = providersByCardId;
    }

    public Task<DashboardCardViewModel?> BuildAsync(
        DashboardCardDefinition definition,
        DashboardCardBuildContext context,
        CancellationToken cancellationToken)
        => _providers.TryGetValue(definition.Id, out var provider)
            ? provider.BuildAsync(definition, context, cancellationToken)
            : Task.FromResult<DashboardCardViewModel?>(null);
}

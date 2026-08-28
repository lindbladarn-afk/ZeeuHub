// Verifies that dashboard cards are routed to one unambiguous feature provider.
using WebApp.Models.Dashboard;
using WebApp.Services.Dashboard;

namespace WebApp.Tests;

public sealed class DashboardCardProviderRegistryTests
{
    [Fact]
    public void Constructor_Rejects_Duplicate_Card_Registrations()
    {
        var providers = new IDashboardCardProvider[]
        {
            new StubProvider("revenue"),
            new StubProvider("revenue")
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => new DashboardCardProviderRegistry(providers));

        Assert.Contains("ingen unik provider", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_Routes_The_Card_To_Its_Provider()
    {
        var provider = new StubProvider("revenue");
        var registry = new DashboardCardProviderRegistry([provider]);
        var definition = new DashboardCardDefinition
        {
            Id = "revenue",
            Title = "Omsättning"
        };

        var result = await registry.BuildAsync(definition, null!, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("revenue", result.Id);
        Assert.Equal(1, provider.BuildCount);
    }

    [Fact]
    public async Task BuildAsync_Returns_Null_For_An_Unregistered_Card()
    {
        var registry = new DashboardCardProviderRegistry([]);
        var definition = new DashboardCardDefinition { Id = "unknown" };

        var result = await registry.BuildAsync(definition, null!, CancellationToken.None);

        Assert.Null(result);
    }

    private sealed class StubProvider(params string[] cardIds) : IDashboardCardProvider
    {
        public IReadOnlyCollection<string> CardIds { get; } = cardIds;
        public int BuildCount { get; private set; }

        public Task<DashboardCardViewModel?> BuildAsync(
            DashboardCardDefinition definition,
            DashboardCardBuildContext context,
            CancellationToken cancellationToken)
        {
            BuildCount++;
            return Task.FromResult<DashboardCardViewModel?>(new DashboardCardViewModel
            {
                Id = definition.Id,
                Title = definition.Title
            });
        }
    }
}

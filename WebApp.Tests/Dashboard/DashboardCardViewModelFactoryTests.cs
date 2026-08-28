// Verifies the shared dashboard card metadata, loading state, and update timestamp conventions.
using WebApp.Models.Dashboard;
using WebApp.Services.Dashboard;

namespace WebApp.Tests;

public sealed class DashboardCardViewModelFactoryTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 7, 18, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_Applies_Shared_Metadata_And_The_Provided_Clock()
    {
        var factory = new DashboardCardViewModelFactory(new FixedTimeProvider(FixedNow));
        var definition = CreateDefinition();

        var card = factory.Create(definition, new object());

        Assert.Equal(definition.Id, card.Id);
        Assert.Equal(definition.Title, card.Title);
        Assert.Equal(definition.SupportedSizes, card.SupportedSizes);
        Assert.Equal(DashboardCardState.Ready, card.State);
        Assert.Equal(FixedNow.UtcDateTime, card.LastUpdatedAtUtc);
    }

    [Fact]
    public void Loading_Uses_The_Shared_Loading_Copy_Without_A_False_Update_Time()
    {
        var factory = new DashboardCardViewModelFactory(new FixedTimeProvider(FixedNow));

        var card = factory.Loading(CreateDefinition(), new object(), "/Member/DashboardCard?cardId=revenue");

        Assert.Equal(DashboardCardState.Loading, card.State);
        Assert.Equal("Laddar omsättning", card.StateTitle);
        Assert.Equal("Hämtar den senaste informationen.", card.StateMessage);
        Assert.Null(card.LastUpdatedAtUtc);
        Assert.NotNull(card.LazyLoadUrl);
    }

    private static DashboardCardDefinition CreateDefinition()
        => new()
        {
            Id = DashboardCardIds.Revenue,
            Title = "Omsättning",
            SupportedSizes = [DashboardWidgetSize.Compact, DashboardWidgetSize.Wide]
        };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

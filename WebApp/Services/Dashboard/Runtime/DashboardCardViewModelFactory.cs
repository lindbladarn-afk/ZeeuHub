// Applies shared metadata and state conventions to every dashboard card view model.
using WebApp.Models.Dashboard;

namespace WebApp.Services.Dashboard;

public sealed class DashboardCardViewModelFactory
{
    private readonly TimeProvider _timeProvider;

    public DashboardCardViewModelFactory(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public DashboardCardViewModel Create(
        DashboardCardDefinition definition,
        object data,
        DashboardCardState state = DashboardCardState.Ready,
        string? stateTitle = null,
        string? stateMessage = null,
        string? lazyLoadUrl = null,
        bool includeUpdatedAt = true)
        => new()
        {
            Id = definition.Id,
            Title = definition.Title,
            SortOrder = definition.SortOrder,
            RenderViewName = definition.RenderViewName,
            ColumnCssClass = definition.ColumnCssClass,
            Size = definition.DefaultSize,
            SupportedSizes = definition.SupportedSizes,
            LazyLoadUrl = lazyLoadUrl,
            State = state,
            StateTitle = stateTitle,
            StateMessage = stateMessage,
            LastUpdatedAtUtc = includeUpdatedAt ? _timeProvider.GetUtcNow().UtcDateTime : null,
            Data = data
        };

    public DashboardCardViewModel Loading(DashboardCardDefinition definition, object data, string? lazyLoadUrl)
        => Create(
            definition,
            data,
            DashboardCardState.Loading,
            $"Laddar {definition.Title.ToLowerInvariant()}",
            "Hämtar den senaste informationen.",
            lazyLoadUrl,
            includeUpdatedAt: false);
}

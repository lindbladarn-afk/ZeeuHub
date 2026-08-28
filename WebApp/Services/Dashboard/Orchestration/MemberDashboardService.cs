// Orchestrates dashboard scope, layout, shared request data, and isolated card providers.
using Entities.Application;
using WebApp.Models.Dashboard;
using WebApp.Services.Application;
using WebApp.Services.Dashboard.Demo;

namespace WebApp.Services.Dashboard;

public sealed class MemberDashboardService : IMemberDashboardService
{
    private static readonly IReadOnlySet<string> RevenueCardIds = new HashSet<string>(
        [
            DashboardCardIds.Revenue,
            DashboardCardIds.RevenueSummary,
            DashboardCardIds.AverageOrderValue,
            DashboardCardIds.RevenueTrend,
            DashboardCardIds.TopSellers
        ],
        StringComparer.Ordinal);

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDashboardConfigurationService _configurationService;
    private readonly IDashboardWidgetLayoutService _layoutService;
    private readonly IDashboardDemoDataService _demoData;
    private readonly IJeevesRuntimeContextService _runtimeContextService;
    private readonly DashboardCardDataContextFactory _dataContextFactory;
    private readonly DashboardCardProviderRegistry _providerRegistry;
    private readonly LinkGenerator _linkGenerator;

    public MemberDashboardService(
        IHttpContextAccessor httpContextAccessor,
        IDashboardConfigurationService configurationService,
        IDashboardWidgetLayoutService layoutService,
        IDashboardDemoDataService demoData,
        IJeevesRuntimeContextService runtimeContextService,
        DashboardCardDataContextFactory dataContextFactory,
        DashboardCardProviderRegistry providerRegistry,
        LinkGenerator linkGenerator)
    {
        _httpContextAccessor = httpContextAccessor;
        _configurationService = configurationService;
        _layoutService = layoutService;
        _demoData = demoData;
        _runtimeContextService = runtimeContextService;
        _dataContextFactory = dataContextFactory;
        _providerRegistry = providerRegistry;
        _linkGenerator = linkGenerator;
    }

    public async Task<MemberDashboardPageViewModel> BuildAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync(isSingleCardRequest: false, cancellationToken);
        var cards = await BuildCardsAsync(request.Definitions, request.Context, cancellationToken);
        var revenueAnalysis = request.Definitions.Any(definition => RevenueCardIds.Contains(definition.Id))
            ? (await request.Context.Data.GetRevenueAsync(cancellationToken)).Value.Analysis
            : new RevenueAnalysisContext();

        return new MemberDashboardPageViewModel
        {
            ActiveCompanyName = request.Context.RuntimeContext?.CompanyName
                ?? request.Context.User?.CompanyName
                ?? string.Empty,
            ActiveCompanyCode = request.Context.RuntimeContext?.CompanyCode,
            HasDataAccess = request.HasDataAccess,
            DataAccessWarning = request.Context.User?.DataAccessWarning,
            RevenueAnalysis = revenueAnalysis,
            Cards = cards,
            AvailableCards = request.AvailableCards
        };
    }

    public async Task<DashboardCardViewModel?> BuildCardAsync(
        string cardId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cardId))
        {
            return null;
        }

        var request = await CreateRequestAsync(isSingleCardRequest: true, cancellationToken);
        var definition = request.Definitions.FirstOrDefault(item =>
            string.Equals(item.Id, cardId.Trim(), StringComparison.Ordinal));

        return definition is null
            ? null
            : await _providerRegistry.BuildAsync(definition, request.Context, cancellationToken);
    }

    private async Task<DashboardBuildRequest> CreateRequestAsync(
        bool isSingleCardRequest,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.Session.Get<UserSession>("UserObject");
        var hasDataAccess = user?.HasDataAccess == true;
        var availableCards = (await _configurationService.GetAvailableCardsAsync(user, cancellationToken))
            .Where(card => card.Enabled)
            .Where(card => hasDataAccess || !card.RequiresDataAccess)
            .OrderBy(card => card.SortOrder)
            .ToList();
        var layout = await _layoutService.GetLayoutAsync(
            user,
            _configurationService.GetDefaultLayout(user),
            cancellationToken);
        var definitions = ComposeDefinitions(availableCards, layout);

        JeevesRuntimeContext? runtimeContext = null;
        if (hasDataAccess && definitions.Any(definition => definition.RequiresDataAccess))
        {
            var result = await _runtimeContextService.ResolveAsync(user, cancellationToken);
            runtimeContext = result.Success ? result.Value : null;
        }

        var data = _dataContextFactory.Create(runtimeContext);
        var context = new DashboardCardBuildContext(
            user,
            runtimeContext,
            _demoData.ShouldUseDemoData(user),
            isSingleCardRequest,
            httpContext,
            data,
            _linkGenerator);

        return new DashboardBuildRequest(
            context,
            definitions,
            availableCards,
            hasDataAccess);
    }

    private async Task<IReadOnlyList<DashboardCardViewModel>> BuildCardsAsync(
        IReadOnlyList<DashboardCardDefinition> definitions,
        DashboardCardBuildContext context,
        CancellationToken cancellationToken)
    {
        var cardTasks = definitions.Select(definition =>
            _providerRegistry.BuildAsync(definition, context, cancellationToken));
        var cards = await Task.WhenAll(cardTasks);

        return cards
            .Where(card => card is not null)
            .Cast<DashboardCardViewModel>()
            .OrderBy(card => card.SortOrder)
            .ToList();
    }

    private static IReadOnlyList<DashboardCardDefinition> ComposeDefinitions(
        IReadOnlyList<DashboardCardDefinition> availableCards,
        IReadOnlyList<DashboardWidgetLayout> layout)
    {
        var definitionsById = availableCards.ToDictionary(card => card.Id, StringComparer.Ordinal);

        return layout
            .Where(item => item.IsVisible)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.WidgetId, StringComparer.Ordinal)
            .Select(item => definitionsById.TryGetValue(item.WidgetId, out var definition)
                ? ApplyLayout(definition, item)
                : null)
            .Where(definition => definition is not null)
            .Cast<DashboardCardDefinition>()
            .ToList();
    }

    private static DashboardCardDefinition ApplyLayout(
        DashboardCardDefinition definition,
        DashboardWidgetLayout layout)
    {
        var size = definition.SupportedSizes.Contains(layout.Size)
            ? layout.Size
            : definition.DefaultSize;

        return new DashboardCardDefinition
        {
            Id = definition.Id,
            Title = definition.Title,
            Description = definition.Description,
            Category = definition.Category,
            SortOrder = layout.SortOrder,
            RenderViewName = definition.RenderViewName,
            ColumnCssClass = GetColumnCssClass(size),
            DefaultSize = size,
            SupportedSizes = definition.SupportedSizes,
            RequiresDataAccess = definition.RequiresDataAccess,
            PermissionIds = definition.PermissionIds,
            Enabled = definition.Enabled
        };
    }

    private static string GetColumnCssClass(DashboardWidgetSize size)
        => size switch
        {
            DashboardWidgetSize.Full => "col-12",
            DashboardWidgetSize.Wide => "col-xl-8 col-lg-6 col-md-6",
            _ => "col-xl-4 col-lg-6 col-md-6"
        };

    private sealed record DashboardBuildRequest(
        DashboardCardBuildContext Context,
        IReadOnlyList<DashboardCardDefinition> Definitions,
        IReadOnlyList<DashboardCardDefinition> AvailableCards,
        bool HasDataAccess);
}

// Verifies that the member dashboard controller remains healthy when dependent data sources fail.
using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using WebApp.Controllers;
using WebApp.Helpers;
using WebApp.Models.Application;
using WebApp.Models.Dashboard;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.Services.Dashboard;
using WebApp.Services.Integration;
using WebApp.Services.Invoices;
using WebApp.Services.Orders;
using WebApp.Services;
using WebApp.ViewModels.Shared;

namespace WebApp.Tests;

// Member controller tests cover customer-facing integration health responses.
public sealed class MemberControllerHealthTests
{
    [Fact]
    public async Task DashboardGrid_Returns_Replaceable_Grid_Partial()
    {
        var expectedCards = new[]
        {
            new DashboardCardViewModel
            {
                Id = DashboardCardIds.ActionCenter,
                Title = "Action Center",
                RenderViewName = "Dashboard/Cards/_ActionCenterCard"
            }
        };
        var controller = CreateController(new NoopMemberDashboardService(expectedCards));

        var result = await controller.DashboardGrid(CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("~/Views/Member/Dashboard/_DashboardGrid.cshtml", partial.ViewName);
        Assert.Same(expectedCards, partial.Model);
    }

    [Fact]
    public async Task DashboardCard_Returns_One_Refreshable_Card_Partial()
    {
        var expectedCard = new DashboardCardViewModel
        {
            Id = DashboardCardIds.RevenueSummary,
            Title = "Omsättning",
            RenderViewName = "Dashboard/Cards/_RevenueMetricCard"
        };
        var controller = CreateController(new NoopMemberDashboardService([expectedCard]));

        var result = await controller.DashboardCard(DashboardCardIds.RevenueSummary, CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("~/Views/Member/Dashboard/_DashboardCardContent.cshtml", partial.ViewName);
        Assert.Same(expectedCard, partial.Model);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task DashboardCard_Rejects_Invalid_Card_Id(string? cardId)
    {
        var controller = CreateController();

        var result = await controller.DashboardCard(cardId, CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task RunIntegrationHealth_Returns_Generic_Message_When_Centra_Check_Fails()
    {
        var controller = CreateController();

        var result = await controller.RunIntegrationHealth(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.DoesNotContain("authorization=secret-value", payload, StringComparison.OrdinalIgnoreCase);
    }

    private static MemberController CreateController(IMemberDashboardService? memberDashboardService = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature());
        httpContext.Session.Set("UserObject", new UserSession
        {
            UserId = "user-1",
            Email = "user@example.com",
            CompanyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            JeevesActiveCompany = 1000,
            CompanyName = "Acme"
        });

        var integrationOptions = Options.Create(new IntegrationOptions
        {
            Companies =
            [
                new IntegrationCompanyConfig
                {
                    CompanyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Enabled = true,
                    Sources =
                    [
                        new IntegrationSourceConfig
                        {
                            Source = IntegrationSource.Centra,
                            BaseUrl = "https://centra.invalid"
                        }
                    ]
                }
            ]
        });

        var controller = new MemberController(
            memberDashboardService ?? new NoopMemberDashboardService(),
            new NoopDashboardWidgetLayoutService(),
            new NoopDashboardConfigurationService(),
            new NoopIntegrationSyncService(),
            integrationOptions,
            Options.Create(new AkeneoOptions()),
            new ThrowingHttpClientFactory(),
            new NoopJeevesAuthService(),
            new HttpContextAccessor { HttpContext = httpContext },
            new NoopSidebarRuntimeStatusService(),
            new DummyStringLocalizer());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    private sealed class ThrowingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(new ThrowingHandler());

        private sealed class ThrowingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => throw new InvalidOperationException("authorization=secret-value");
        }
    }

    private sealed class NoopMemberDashboardService : IMemberDashboardService
    {
        private readonly IReadOnlyList<DashboardCardViewModel> _cards;

        public NoopMemberDashboardService(IReadOnlyList<DashboardCardViewModel>? cards = null)
        {
            _cards = cards ?? [];
        }

        public Task<MemberDashboardPageViewModel> BuildAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new MemberDashboardPageViewModel { Cards = _cards });

        public Task<DashboardCardViewModel?> BuildCardAsync(string cardId, CancellationToken cancellationToken = default)
            => Task.FromResult(_cards.FirstOrDefault(card => string.Equals(card.Id, cardId, StringComparison.Ordinal)));
    }

    private sealed class NoopDashboardWidgetLayoutService : IDashboardWidgetLayoutService
    {
        public Task<IReadOnlyList<DashboardWidgetLayout>> GetLayoutAsync(UserSession? user, IReadOnlyList<DashboardWidgetLayout> defaultLayout, CancellationToken cancellationToken = default)
            => Task.FromResult(defaultLayout);

        public Task SaveAsync(UserSession user, IReadOnlyList<DashboardWidgetLayout> widgets, IReadOnlyCollection<DashboardCardDefinition> allowedCards, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ResetAsync(UserSession user, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoopDashboardConfigurationService : IDashboardConfigurationService
    {
        public Task<IReadOnlyList<DashboardCardDefinition>> GetAvailableCardsAsync(UserSession? user, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DashboardCardDefinition>>([]);
        public IReadOnlyList<DashboardWidgetLayout> GetDefaultLayout(UserSession? user) => [];
    }

    private sealed class NoopIntegrationSyncService : IIntegrationSyncService
    {
        public Task<IntegrationSyncResult> SyncCompanyAsync(Guid companyId, string? externalOrderId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(new IntegrationSyncResult());
    }

    private sealed class NoopJeevesAuthService : IJeevesAuthService
    {
        public Task<string?> GetAccessTokenAsync(string cacheKey, string authUrl, string appId, string appSecret, CancellationToken ct = default)
            => Task.FromResult<string?>(string.Empty);

        public void Invalidate(string cacheKey) { }
    }

    private sealed class NoopSidebarRuntimeStatusService : ISidebarRuntimeStatusService
    {
        public SidebarRuntimeStatusViewModel GetStatus(UserSession? sessionUser) => new();
        public Task<SidebarRuntimeStatusViewModel> GetStatusAsync(UserSession? sessionUser, CancellationToken cancellationToken = default) => Task.FromResult(new SidebarRuntimeStatusViewModel());
        public void RecordEvent(UserSession sessionUser, SidebarRuntimeEventRecord record) { }
        public void RecordEvent(Guid companyId, SidebarRuntimeEventRecord record) { }
        public void MarkAllRead(UserSession sessionUser) { }
    }

    private sealed class DummyStringLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, $"{name}: {string.Join(", ", arguments)}");
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private sealed class TestSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new TestSession();
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new();
        public bool IsAvailable => true;
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[]? value) => _values.TryGetValue(key, out value);
    }
}

// Verifies durable, company-isolated dashboard block preferences and default composition.
using Entities.Application;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Dashboard;
using WebApp.Services.Application;
using WebApp.Services.Dashboard;

namespace WebApp.Tests;

public sealed class DashboardWidgetLayoutServiceTests
{
    [Fact]
    public async Task SaveAsync_Persists_Visible_Widgets_Per_User_And_Company()
    {
        var factory = new TestDbContextFactory();
        var service = new DashboardWidgetLayoutService(factory);
        var user = CreateUser();
        var allowed = BuildAllowedCards(DashboardCardIds.ActionCenter, DashboardCardIds.Revenue, DashboardCardIds.NotifyMe);

        await service.SaveAsync(
            user,
            [
                new DashboardWidgetLayout
                {
                    WidgetId = DashboardCardIds.NotifyMe,
                    SortOrder = 10,
                    Size = DashboardWidgetSize.Wide,
                    IsVisible = true
                },
                new DashboardWidgetLayout
                {
                    WidgetId = DashboardCardIds.ActionCenter,
                    SortOrder = 20,
                    Size = DashboardWidgetSize.Full,
                    IsVisible = true
                }
            ],
            allowed);

        var layout = await service.GetLayoutAsync(user, BuildDefaultLayout());

        Assert.Collection(
            layout.OrderBy(item => item.SortOrder),
            item =>
            {
                Assert.Equal(DashboardCardIds.NotifyMe, item.WidgetId);
                Assert.True(item.IsVisible);
                Assert.Equal(DashboardWidgetSize.Wide, item.Size);
            },
            item =>
            {
                Assert.Equal(DashboardCardIds.ActionCenter, item.WidgetId);
                Assert.True(item.IsVisible);
                Assert.Equal(DashboardWidgetSize.Full, item.Size);
            },
            item =>
            {
                Assert.Equal(DashboardCardIds.Revenue, item.WidgetId);
                Assert.False(item.IsVisible);
            });
    }

    [Fact]
    public async Task GetLayoutAsync_Returns_Default_When_Company_Has_No_Preference()
    {
        var factory = new TestDbContextFactory();
        var service = new DashboardWidgetLayoutService(factory);
        var user = CreateUser();
        var anotherCompany = CreateUser();
        anotherCompany.CompanyId = Guid.NewGuid();

        await service.SaveAsync(
            user,
            [new DashboardWidgetLayout { WidgetId = DashboardCardIds.NotifyMe, SortOrder = 10, Size = DashboardWidgetSize.Compact, IsVisible = true }],
            BuildAllowedCards(DashboardCardIds.ActionCenter, DashboardCardIds.NotifyMe));

        var layout = await service.GetLayoutAsync(anotherCompany, BuildDefaultLayout());

        Assert.Equal(DashboardCardIds.ActionCenter, Assert.Single(layout).WidgetId);
    }

    [Fact]
    public async Task GetLayoutAsync_Does_Not_Share_Layout_Between_Users_In_The_Same_Company()
    {
        var factory = new TestDbContextFactory();
        var service = new DashboardWidgetLayoutService(factory);
        var firstUser = CreateUser();
        var secondUser = CreateUser();
        secondUser.UserId = "user-2";

        await service.SaveAsync(
            firstUser,
            [new DashboardWidgetLayout { WidgetId = DashboardCardIds.NotifyMe, SortOrder = 10, Size = DashboardWidgetSize.Wide, IsVisible = true }],
            BuildAllowedCards(DashboardCardIds.ActionCenter, DashboardCardIds.NotifyMe));

        var firstLayout = await service.GetLayoutAsync(firstUser, BuildDefaultLayout());
        var secondLayout = await service.GetLayoutAsync(secondUser, BuildDefaultLayout());

        Assert.Equal(DashboardCardIds.NotifyMe, firstLayout.Single(item => item.IsVisible).WidgetId);
        Assert.Equal(DashboardCardIds.ActionCenter, Assert.Single(secondLayout).WidgetId);
    }

    [Fact]
    public async Task SaveAsync_Rejects_Unknown_Widget()
    {
        var service = new DashboardWidgetLayoutService(new TestDbContextFactory());

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(
            CreateUser(),
            [new DashboardWidgetLayout { WidgetId = "unknown", SortOrder = 10, Size = DashboardWidgetSize.Compact, IsVisible = true }],
            BuildAllowedCards(DashboardCardIds.ActionCenter)));

        Assert.Equal("Ett ogiltigt block skickades för startsidan.", exception.Message);
    }

    [Fact]
    public async Task SaveAsync_Rejects_Size_That_The_Card_Does_Not_Support()
    {
        var service = new DashboardWidgetLayoutService(new TestDbContextFactory());
        var allowed = new[]
        {
            new DashboardCardDefinition
            {
                Id = DashboardCardIds.RevenueTrend,
                SupportedSizes = [DashboardWidgetSize.Wide, DashboardWidgetSize.Full]
            }
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(
            CreateUser(),
            [new DashboardWidgetLayout { WidgetId = DashboardCardIds.RevenueTrend, SortOrder = 10, Size = DashboardWidgetSize.Compact }],
            allowed));

        Assert.Equal("Den valda storleken stöds inte av blocket.", exception.Message);
    }

    [Fact]
    public async Task SaveAsync_Stores_A_Supported_Default_Size_For_A_Hidden_Card()
    {
        var factory = new TestDbContextFactory();
        var service = new DashboardWidgetLayoutService(factory);
        var actionCenter = new DashboardCardDefinition
        {
            Id = DashboardCardIds.ActionCenter,
            DefaultSize = DashboardWidgetSize.Full,
            SupportedSizes = [DashboardWidgetSize.Wide, DashboardWidgetSize.Full]
        };

        await service.SaveAsync(CreateUser(), [], [actionCenter]);

        await using var db = factory.CreateDbContext();
        var stored = await db.DashboardWidgetPreferences!.SingleAsync();

        Assert.False(stored.IsVisible);
        Assert.Equal(DashboardWidgetSize.Full.ToString(), stored.Size);
    }

    [Fact]
    public async Task ResetAsync_Removes_Personal_Layout_And_Restores_Default()
    {
        var service = new DashboardWidgetLayoutService(new TestDbContextFactory());
        var user = CreateUser();

        await service.SaveAsync(
            user,
            [new DashboardWidgetLayout { WidgetId = DashboardCardIds.NotifyMe, SortOrder = 10, Size = DashboardWidgetSize.Compact, IsVisible = true }],
            BuildAllowedCards(DashboardCardIds.ActionCenter, DashboardCardIds.NotifyMe));

        await service.ResetAsync(user);

        var layout = await service.GetLayoutAsync(user, BuildDefaultLayout());

        Assert.Equal(DashboardCardIds.ActionCenter, Assert.Single(layout).WidgetId);
    }

    [Fact]
    public void DefaultDashboardConfiguration_Uses_Actionable_Layout_By_Default()
    {
        var service = new DefaultDashboardConfigurationService(new StubCompanyPermissionGuard([]));

        var layout = service.GetDefaultLayout(CreateUser());

        Assert.Equal(
            [
                DashboardCardIds.ActionCenter,
                DashboardCardIds.Revenue,
                DashboardCardIds.AverageOrderValue,
                DashboardCardIds.RevenueTrend,
                DashboardCardIds.TopSellers,
                DashboardCardIds.InvoiceSummary,
                DashboardCardIds.DeliveryStatus,
                DashboardCardIds.NotifyMe
            ],
            layout.OrderBy(item => item.SortOrder).Select(item => item.WidgetId).ToArray());
    }

    [Fact]
    public async Task DefaultDashboardConfiguration_Filters_Module_Bound_Cards_By_Company_Permission()
    {
        var service = new DefaultDashboardConfigurationService(new StubCompanyPermissionGuard([PortalModuleIds.InvoicesSubModule]));

        var cards = await service.GetAvailableCardsAsync(CreateUser());

        Assert.Contains(cards, card => card.Id == DashboardCardIds.InvoiceSummary);
        Assert.Contains(cards, card => card.Id == DashboardCardIds.OverdueInvoices);
        Assert.DoesNotContain(cards, card => card.Id == DashboardCardIds.BankReconciliation);
        Assert.DoesNotContain(cards, card => card.Id == DashboardCardIds.DeliveryStatus);
        Assert.DoesNotContain(cards, card => card.Id == DashboardCardIds.DocumentSigning);
        Assert.All(cards, card => Assert.False(string.IsNullOrWhiteSpace(card.Category)));
    }

    [Fact]
    public async Task DefaultDashboardConfiguration_Allows_DocumentSigning_At_Full_Width()
    {
        var service = new DefaultDashboardConfigurationService(
            new StubCompanyPermissionGuard([PortalModuleIds.DocumentSigningSubModule]));

        var cards = await service.GetAvailableCardsAsync(CreateUser());
        var documentSigning = Assert.Single(cards, card => card.Id == DashboardCardIds.DocumentSigning);

        Assert.Contains(DashboardWidgetSize.Full, documentSigning.SupportedSizes);
    }

    private static IReadOnlyList<DashboardWidgetLayout> BuildDefaultLayout()
        => [new DashboardWidgetLayout { WidgetId = DashboardCardIds.ActionCenter, SortOrder = 10, Size = DashboardWidgetSize.Full, IsVisible = true }];

    private static IReadOnlyList<DashboardCardDefinition> BuildAllowedCards(params string[] cardIds)
        => cardIds.Select(cardId => new DashboardCardDefinition { Id = cardId }).ToList();

    private static UserSession CreateUser()
        => new()
        {
            UserId = "user-1",
            CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CompanyName = "Testbolag",
            PersSign = "PERSIGN",
            JeevesActiveCompany = 123
        };

    private sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"dashboard-layout-{Guid.NewGuid():N}")
            .Options;

        public TestDbContextFactory()
        {
            using var db = CreateDbContext();
            db.Database.EnsureCreated();
        }

        public ApplicationDbContext CreateDbContext() => new(_options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class StubCompanyPermissionGuard : ICompanyPermissionGuard
    {
        private readonly IReadOnlyCollection<Guid> _allowedPermissionIds;

        public StubCompanyPermissionGuard(IReadOnlyCollection<Guid> allowedPermissionIds)
        {
            _allowedPermissionIds = allowedPermissionIds;
        }

        public Task<bool> HasAccessAsync(Guid companyId, Guid subModuleId, CancellationToken cancellationToken = default)
            => Task.FromResult(_allowedPermissionIds.Contains(subModuleId));
    }
}

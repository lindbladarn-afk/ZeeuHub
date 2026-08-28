using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Entities.Application;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Data;
using WebApp.Models.ActionCenter;
using WebApp.Models.Application;
using WebApp.Models.BackgroundJobs;
using WebApp.Models.Integration;
using WebApp.Services.ActionCenter;
using WebApp.Services.Application;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Services.ExcelImport;
using WebApp.Services.Integration.FlowEngine;

namespace WebApp.Tests;

// Verifies the sidebar runtime status can surface transient Excel Import items without a database roundtrip.
public sealed class SidebarRuntimeStatusServiceTests
{
    [Fact]
    public void GetStatus_Includes_Transient_ExcelImport_Items()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var companyId = Guid.NewGuid();
        var transientStore = new ExcelImportTransientStatusStore(cache);
        transientStore.Record(new SidebarRuntimeEventRecord
        {
            CompanyId = companyId,
            AggregateKey = "excel-import:budget:job-1",
            Source = "ExcelImport",
            Title = "Budget",
            Summary = "Import av budget.xlsx väntar på att starta.",
            LinkUrl = "/ExcelImport",
            StatusLabel = "Queued",
            StatusTone = "info",
            IconClass = "fas fa-file-excel",
            OccurredAtUtc = DateTimeOffset.UtcNow
        });

        var service = new SidebarRuntimeStatusService(
            new HttpContextAccessor { HttpContext = CreateHttpContext() },
            new FakeActionCenterService(),
            new EmptyFlowEngineJobStore(),
            new EmptyBackgroundJobRuntimeEventStore(),
            transientStore,
            new NullLinkGenerator(),
            new ThrowingDbContextFactory(),
            new DummyStringLocalizer(),
            NullLogger<SidebarRuntimeStatusService>.Instance);

        var result = service.GetStatus(new UserSession
        {
            CompanyId = companyId,
            UserId = string.Empty
        });

        Assert.True(result.IsVisible);
        Assert.Equal(1, result.NotificationCount);
        Assert.Single(result.NotificationItems);
        Assert.Equal("Budget", result.LatestItem?.Title);
        Assert.Equal("Budget", result.NotificationItems[0].Title);
    }

    [Fact]
    public async Task GetStatusAsync_Summarizes_ActionCenter_Items_Without_Counting_Them_As_Unread_Notifications()
    {
        var companyId = Guid.NewGuid();
        var actionCenter = new FakeActionCenterService(
            new ActionCenterInsight
            {
                Key = "purchase-approval:1",
                Category = "Attest",
                Title = "Inköpsorder 600002 väntar på godkännande",
                Description = "Godkänn väntande inköpsorder-attester.",
                Priority = ActionCenterPriority.Medium,
                DetectedAt = DateTime.UtcNow.AddMinutes(-5),
                LinkUrl = "/WebApproval/PurchaseApproval"
            },
            new ActionCenterInsight
            {
                Key = "invoice:1",
                Category = "Fakturor",
                Title = "4 obetalda fakturor",
                Description = "Fakturor kräver uppföljning.",
                Priority = ActionCenterPriority.High,
                DetectedAt = DateTime.UtcNow.AddMinutes(-10),
                LinkUrl = "/ActionCenter"
            });

        var service = new SidebarRuntimeStatusService(
            new HttpContextAccessor { HttpContext = CreateHttpContext() },
            actionCenter,
            new EmptyFlowEngineJobStore(),
            new EmptyBackgroundJobRuntimeEventStore(),
            new ExcelImportTransientStatusStore(new MemoryCache(new MemoryCacheOptions())),
            new NullLinkGenerator(),
            new ThrowingDbContextFactory(),
            new DummyStringLocalizer(),
            NullLogger<SidebarRuntimeStatusService>.Instance);

        var result = await service.GetStatusAsync(new UserSession
        {
            CompanyId = companyId,
            UserId = string.Empty
        });

        Assert.Equal(0, result.NotificationCount);
        Assert.Empty(result.NotificationItems);
        Assert.NotNull(result.ActionCenterSummaryItem);
        Assert.Equal("2 öppna åtgärder", result.ActionCenterSummaryItem.Title);
        Assert.Equal("Action Center", result.ActionCenterSummaryItem.Source);
        Assert.Equal("/ActionCenter", result.ActionCenterSummaryItem.LinkUrl);
        Assert.Equal("action-center:summary", result.ActionCenterSummaryItem.AggregateKey);
        Assert.Equal("danger", result.ActionCenterSummaryItem.StatusTone);
        Assert.Contains("1 med hög prioritet", result.ActionCenterSummaryItem.Summary);
    }

    private sealed class EmptyFlowEngineJobStore : IFlowEngineJobStore
    {
        public FlowEngineJobSnapshot Create(Guid companyId, string? userId, string? userName, string[] arguments, FlowEngineExecuteJobRequest request)
            => throw new NotSupportedException();

        public FlowEngineJobSnapshot MarkRunning(Guid companyId, Guid jobId, DateTimeOffset startedAtUtc)
            => throw new NotSupportedException();

        public FlowEngineJobSnapshot Complete(Guid companyId, Guid jobId, FlowEngineJobResultPayload result)
            => throw new NotSupportedException();

        public FlowEngineJobSnapshot Fail(Guid companyId, Guid jobId, FlowEngineJobResultPayload result, string errorMessage)
            => throw new NotSupportedException();

        public FlowEngineJobSnapshot? Get(Guid companyId, Guid jobId) => null;

        public IReadOnlyList<FlowEngineJobSnapshot> ListRecent(Guid companyId, int take) => Array.Empty<FlowEngineJobSnapshot>();

        public FlowEngineHistoryPageResult ListPage(Guid companyId, int page, int pageSize, string? systemKey = null, FlowEngineHistoryFilterState? filters = null)
            => new() { Jobs = Array.Empty<FlowEngineJobSnapshot>(), CurrentPage = page, PageSize = pageSize, TotalCount = 0 };
    }

    private sealed class FakeActionCenterService : IActionCenterService
    {
        private readonly IReadOnlyList<ActionCenterInsight> _insights;

        public FakeActionCenterService(params ActionCenterInsight[] insights)
        {
            _insights = insights;
        }

        public Task<ActionCenterViewModel> GetInsightsAsync(UserSession user, int take, CancellationToken cancellationToken)
            => Task.FromResult(new ActionCenterViewModel
            {
                TotalCount = _insights.Count,
                Insights = _insights.Take(take).ToList()
            });

        public Task<ActionCenterSummaryDto> GetSummaryAsync(UserSession user, CancellationToken cancellationToken)
            => Task.FromResult(new ActionCenterSummaryDto
            {
                Count = _insights.Count,
                HasHighPriority = _insights.Any(item => item.Priority == ActionCenterPriority.High),
                LatestDetectedAt = _insights.Count == 0 ? null : _insights.Max(item => item.DetectedAt)
            });

        public void InvalidateCache(UserSession user)
        {
        }
    }

    private sealed class EmptyBackgroundJobRuntimeEventStore : IBackgroundJobRuntimeEventStore
    {
        public void Record(BackgroundJobRuntimeEventRecord record)
        {
        }

        public IReadOnlyList<BackgroundJobRuntimeEventRecord> ListRecent(Guid companyId, int take)
            => Array.Empty<BackgroundJobRuntimeEventRecord>();
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => throw new InvalidOperationException("DB should not be used in this test.");
        public ValueTask<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("DB should not be used in this test.");
    }

    private sealed class DummyStringLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private sealed class NullLinkGenerator : LinkGenerator
    {
        public override string? GetPathByAddress<TAddress>(TAddress address, RouteValueDictionary values, PathString pathBase = default, FragmentString fragment = default, LinkOptions? options = null)
            => null;

        public override string? GetUriByAddress<TAddress>(TAddress address, RouteValueDictionary values, string? scheme, HostString host, PathString pathBase = default, FragmentString fragment = default, LinkOptions? options = null)
            => null;

        public override string? GetPathByAddress<TAddress>(HttpContext httpContext, TAddress address, RouteValueDictionary values, RouteValueDictionary? ambientValues = null, PathString? pathBase = null, FragmentString fragment = default, LinkOptions? options = null)
            => null;

        public override string? GetUriByAddress<TAddress>(HttpContext httpContext, TAddress address, RouteValueDictionary values, RouteValueDictionary? ambientValues = null, string? scheme = null, HostString? host = null, PathString? pathBase = null, FragmentString fragment = default, LinkOptions? options = null)
            => null;
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<ISessionFeature>(new SessionFeature
        {
            Session = new TestSession()
        });
        return context;
    }

    private sealed class SessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new TestSession();
    }

    private sealed class TestSession : ISession
    {
        public bool IsAvailable => true;
        public string Id => Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => Array.Empty<string>();
        public void Clear() { }
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) { }
        public void Set(string key, byte[] value) { }
        public bool TryGetValue(string key, out byte[]? value) { value = null; return false; }
    }
}

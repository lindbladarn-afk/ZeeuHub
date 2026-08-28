using Entities.Application;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Data;
using WebApp.Models.ActionCenter;
using WebApp.Models.Application;
using WebApp.Models.BackgroundJobs;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Services.ActionCenter;
using WebApp.Services.ExcelImport;
using WebApp.Services.Integration.FlowEngine;

namespace WebApp.Tests;

// Verifies transient Excel imports are included in the shared sidebar status.
public sealed class ExcelImportSidebarRuntimeStatusTests
{
    [Fact]
    public void GetStatus_Includes_Transient_ExcelImport()
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
            Summary = "Importen vantar pa att starta.",
            LinkUrl = "/ExcelImport",
            StatusLabel = "Queued",
            StatusTone = "info",
            IconClass = "fas fa-file-excel",
            OccurredAtUtc = DateTimeOffset.UtcNow
        });

        var service = new SidebarRuntimeStatusService(
            new HttpContextAccessor { HttpContext = CreateHttpContext() },
            new EmptyActionCenterService(),
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

        var item = Assert.Single(result.NotificationItems);
        Assert.True(result.IsVisible);
        Assert.Equal("Budget", result.LatestItem?.Title);
        Assert.Equal("Budget", item.Title);
    }

    private sealed class EmptyActionCenterService : IActionCenterService
    {
        public Task<ActionCenterViewModel> GetInsightsAsync(UserSession user, int take, CancellationToken cancellationToken)
            => Task.FromResult(new ActionCenterViewModel());

        public Task<ActionCenterSummaryDto> GetSummaryAsync(UserSession user, CancellationToken cancellationToken)
            => Task.FromResult(new ActionCenterSummaryDto());

        public void InvalidateCache(UserSession user)
        {
        }
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

        public IReadOnlyList<FlowEngineJobSnapshot> ListRecent(Guid companyId, int take)
            => Array.Empty<FlowEngineJobSnapshot>();

        public FlowEngineHistoryPageResult ListPage(Guid companyId, int page, int pageSize, string? systemKey = null, FlowEngineHistoryFilterState? filters = null)
            => new() { Jobs = Array.Empty<FlowEngineJobSnapshot>(), CurrentPage = page, PageSize = pageSize, TotalCount = 0 };
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
        public ApplicationDbContext CreateDbContext()
            => throw new InvalidOperationException("DB should not be used in this test.");

        public ValueTask<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("DB should not be used in this test.");
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
        context.Features.Set<ISessionFeature>(new SessionFeature { Session = new TestSession() });
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
        public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) { value = null; return false; }
    }
}

using Entities.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Routing;
using WebApp.Data;
using WebApp.Controllers;
using WebApp.Models.Application;
using WebApp.Models.ActionCenter;
using WebApp.Models.BackgroundJobs;
using WebApp.Models.Integration;
using WebApp.Services.ActionCenter;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Services.Integration.FlowEngine;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Application;

public sealed class SidebarRuntimeStatusService : ISidebarRuntimeStatusService
{
    private const string ReadAtSessionKeyPrefix = "Sidebar.RuntimeEvents.ReadAt";
    private const int RecentRecordedEventLimit = 100;
    private const int RecentFlowEngineJobLimit = 100;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IActionCenterService _actionCenterService;
    private readonly IFlowEngineJobStore _flowEngineJobStore;
    private readonly IBackgroundJobRuntimeEventStore _backgroundJobRuntimeEventStore;
    private readonly IExcelImportTransientStatusStore _excelImportTransientStatusStore;
    private readonly LinkGenerator _linkGenerator;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IStringLocalizer<SharedResources> _sharedLocalizer;
    private readonly ILogger<SidebarRuntimeStatusService> _logger;

    public SidebarRuntimeStatusService(
        IHttpContextAccessor httpContextAccessor,
        IActionCenterService actionCenterService,
        IFlowEngineJobStore flowEngineJobStore,
        IBackgroundJobRuntimeEventStore backgroundJobRuntimeEventStore,
        IExcelImportTransientStatusStore excelImportTransientStatusStore,
        LinkGenerator linkGenerator,
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IStringLocalizer<SharedResources> sharedLocalizer,
        ILogger<SidebarRuntimeStatusService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _actionCenterService = actionCenterService;
        _flowEngineJobStore = flowEngineJobStore;
        _backgroundJobRuntimeEventStore = backgroundJobRuntimeEventStore;
        _excelImportTransientStatusStore = excelImportTransientStatusStore;
        _linkGenerator = linkGenerator;
        _dbContextFactory = dbContextFactory;
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    public SidebarRuntimeStatusViewModel GetStatus(UserSession? sessionUser)
        => BuildStatus(sessionUser, null);

    public async Task<SidebarRuntimeStatusViewModel> GetStatusAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
    {
        var actionCenterSummaryItem = await GetActionCenterSummaryItemAsync(sessionUser, cancellationToken);
        return BuildStatus(sessionUser, actionCenterSummaryItem);
    }

    private SidebarRuntimeStatusViewModel BuildStatus(
        UserSession? sessionUser,
        SidebarRuntimeStatusItemViewModel? actionCenterSummaryItem)
    {
        if (sessionUser?.CompanyId is not Guid companyId || companyId == Guid.Empty)
            return new SidebarRuntimeStatusViewModel();

        var items = new List<SidebarRuntimeStatusItemViewModel>();
        items.AddRange(GetRecordedItems(companyId));
        items.AddRange(GetFlowEngineItems(companyId));
        items.AddRange(GetExcelImportItems(companyId));

        var ordered = CollapseItems(items)
            .OrderByDescending(item => item.OccurredAtUtc)
            .ToList();
        var latestItem = ordered.FirstOrDefault();
        var lastReadAt = GetLastReadAt(companyId);
        var notificationItems = ordered
            .Where(item => item.OccurredAtUtc > lastReadAt)
            .ToList();
        var unreadCount = notificationItems.Count;

        var activeItem = ordered.FirstOrDefault(IsActiveItem);
        var overallTone = activeItem is not null
            ? "info"
            : string.Equals(latestItem?.StatusTone, "danger", StringComparison.OrdinalIgnoreCase)
                ? "danger"
                : "ready";

        var overallLabel = overallTone switch
        {
            "info" => _sharedLocalizer["SidebarRuntime_OverallRunning"].Value,
            "danger" => _sharedLocalizer["SidebarRuntime_OverallAttention"].Value,
            _ => _sharedLocalizer["SidebarRuntime_OverallReady"].Value
        };

        return new SidebarRuntimeStatusViewModel
        {
            IsVisible = true,
            OverallLabel = overallLabel,
            OverallTone = overallTone,
            RunningLabel = activeItem?.Title ?? _sharedLocalizer["SidebarRuntime_Idle"].Value,
            LatestItem = latestItem,
            LatestLabel = latestItem is { } latest
                ? latest.Title
                : _sharedLocalizer["SidebarRuntime_NoActivityYet"].Value,
            NotificationCount = unreadCount,
            ActionCenterSummaryItem = actionCenterSummaryItem,
            Items = ordered,
            NotificationItems = notificationItems
        };
    }

    private async Task<SidebarRuntimeStatusItemViewModel?> GetActionCenterSummaryItemAsync(
        UserSession? sessionUser,
        CancellationToken cancellationToken)
    {
        if (sessionUser is null)
            return null;

        try
        {
            var model = await _actionCenterService.GetInsightsAsync(sessionUser, 10, cancellationToken);
            var realInsights = model.Insights
                .Where(item => !item.IsMock)
                .ToList();

            if (realInsights.Count == 0)
                return null;

            return MapActionCenterSummary(realInsights);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add Action Center summary to sidebar notifications.");
            return null;
        }
    }

    private SidebarRuntimeStatusItemViewModel MapActionCenterSummary(IReadOnlyList<ActionCenterInsight> insights)
    {
        var latestDetectedAt = insights.Max(item => item.DetectedAt);
        var occurredAt = new DateTimeOffset(DateTime.SpecifyKind(latestDetectedAt, DateTimeKind.Utc));
        var highPriorityCount = insights.Count(item => item.Priority == ActionCenterPriority.High);
        var attestCount = insights.Count(item => string.Equals(item.Category, "Attest", StringComparison.OrdinalIgnoreCase));
        var descriptionParts = insights
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Category) ? "Action Center" : item.Category.Trim())
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(3)
            .Select(group => $"{group.Count()} {group.Key.ToLowerInvariant()}")
            .ToList();
        var summary = descriptionParts.Count == 0
            ? "Öppna åtgärder kräver uppföljning."
            : string.Join(", ", descriptionParts);

        return new SidebarRuntimeStatusItemViewModel
        {
            OccurredAtUtc = occurredAt,
            AggregateKey = "action-center:summary",
            Source = "Action Center",
            Title = $"{insights.Count} öppna åtgärder",
            Summary = highPriorityCount > 0
                ? $"{summary}. {highPriorityCount} med hög prioritet."
                : summary,
            LinkUrl = "/ActionCenter",
            StatusLabel = attestCount > 0 ? $"{attestCount} attest" : "Öppet",
            StatusTone = highPriorityCount > 0 ? "danger" : "warning",
            TimeLabel = ToDisplayTime(occurredAt, "muted", "Väntar"),
            IconClass = "fa fa-bolt"
        };
    }

    public void RecordEvent(UserSession sessionUser, SidebarRuntimeEventRecord record)
    {
        if (sessionUser.CompanyId is not Guid companyId || companyId == Guid.Empty)
            return;

        RecordEvent(companyId, record);
    }

    public void RecordEvent(Guid companyId, SidebarRuntimeEventRecord record)
    {
        if (companyId == Guid.Empty)
            return;

        _backgroundJobRuntimeEventStore.Record(new BackgroundJobRuntimeEventRecord
        {
            CompanyId = companyId,
            EventType = "runtime",
            AggregateKey = record.AggregateKey,
            Source = record.Source,
            Title = record.Title,
            Summary = record.Summary,
            LinkUrl = record.LinkUrl,
            StatusLabel = record.StatusLabel,
            StatusTone = record.StatusTone,
            IconClass = record.IconClass,
            OccurredAtUtc = (record.OccurredAtUtc == default ? DateTimeOffset.UtcNow : record.OccurredAtUtc).UtcDateTime
        });
    }

    public void MarkAllRead(UserSession sessionUser)
    {
        if (sessionUser.CompanyId is not Guid companyId || companyId == Guid.Empty || string.IsNullOrWhiteSpace(sessionUser.UserId))
            return;

        var readAtUtc = DateTime.UtcNow;

        try
        {
            using var db = _dbContextFactory.CreateDbContext();
            var existing = db.SidebarRuntimeNotificationReadStates!
                .SingleOrDefault(item => item.CompanyId == companyId && item.UserId == sessionUser.UserId);

            if (existing is null)
            {
                db.SidebarRuntimeNotificationReadStates!.Add(new SidebarRuntimeNotificationReadStateRecord
                {
                    CompanyId = companyId,
                    UserId = sessionUser.UserId,
                    LastReadAtUtc = readAtUtc,
                    UpdatedAtUtc = readAtUtc
                });
            }
            else
            {
                existing.LastReadAtUtc = readAtUtc;
                existing.UpdatedAtUtc = readAtUtc;
            }

            db.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist sidebar notification read state. Falling back to session.");
            var session = _httpContextAccessor.HttpContext?.Session;
            session?.SetString(GetReadAtKey(companyId, sessionUser.UserId), new DateTimeOffset(readAtUtc, TimeSpan.Zero).ToString("O"));
        }
    }

    private IReadOnlyList<SidebarRuntimeStatusItemViewModel> GetRecordedItems(Guid companyId)
    {
        return _backgroundJobRuntimeEventStore
            .ListRecent(companyId, RecentRecordedEventLimit)
            .OrderByDescending(item => item.OccurredAtUtc)
            .Select(item => new SidebarRuntimeStatusItemViewModel
            {
                OccurredAtUtc = new DateTimeOffset(DateTime.SpecifyKind(item.OccurredAtUtc, DateTimeKind.Utc)),
                AggregateKey = item.AggregateKey,
                Source = item.Source,
                Title = item.Title,
                Summary = item.Summary,
                LinkUrl = item.LinkUrl,
                StatusLabel = item.StatusLabel,
                StatusTone = item.StatusTone,
                TimeLabel = ToDisplayTime(
                    new DateTimeOffset(DateTime.SpecifyKind(item.OccurredAtUtc, DateTimeKind.Utc)),
                    item.StatusTone,
                    item.StatusLabel),
                IconClass = NormalizeIconClass(item.Source, item.LinkUrl, item.IconClass)
            })
            .ToList();
    }

    private IReadOnlyList<SidebarRuntimeStatusItemViewModel> GetFlowEngineItems(Guid companyId)
    {
        return _flowEngineJobStore
            .ListRecent(companyId, RecentFlowEngineJobLimit)
            .Select(job => new SidebarRuntimeStatusItemViewModel
            {
                OccurredAtUtc = job.FinishedAtUtc ?? job.StartedAtUtc ?? job.CreatedAtUtc,
                AggregateKey = $"flowengine-job:{job.Id:N}",
                Source = FlowEngineJobPresentation.GetSystemLabel(job),
                Title = string.IsNullOrWhiteSpace(job.UiLabel) ? (job.Name ?? "FlowEngine-körning") : job.UiLabel,
                Summary = string.IsNullOrWhiteSpace(job.ErrorMessage)
                    ? $"FlowEngine {FlowEngineJobPresentation.GetStatusLabel(job.Status).ToLowerInvariant()}."
                    : job.ErrorMessage,
                LinkUrl = BuildFlowEngineLink(job),
                StatusLabel = FlowEngineJobPresentation.GetStatusLabel(job.Status),
                StatusTone = job.Status switch
                {
                    FlowEngineJobStatus.Running => "info",
                    FlowEngineJobStatus.Queued => "info",
                    FlowEngineJobStatus.Failed => "danger",
                    FlowEngineJobStatus.Succeeded => "success",
                    _ => "muted"
                },
                TimeLabel = ToDisplayTime(
                    job.FinishedAtUtc ?? job.StartedAtUtc ?? job.CreatedAtUtc,
                    job.Status is FlowEngineJobStatus.Running or FlowEngineJobStatus.Queued ? "info" : job.Status switch
                    {
                        FlowEngineJobStatus.Failed => "danger",
                        FlowEngineJobStatus.Succeeded => "success",
                        _ => "muted"
                    },
                    FlowEngineJobPresentation.GetStatusLabel(job.Status)),
                IconClass = NormalizeIconClass(
                    FlowEngineJobPresentation.GetSystemLabel(job),
                    BuildFlowEngineLink(job),
                    "fa fa-bolt")
            })
            .ToList();
    }

    private IReadOnlyList<SidebarRuntimeStatusItemViewModel> GetExcelImportItems(Guid companyId)
    {
        return _excelImportTransientStatusStore
            .ListRecentSummaries(companyId, RecentRecordedEventLimit)
            .OrderByDescending(item => item.OccurredAtUtc)
            .ToList();
    }

    private static string NormalizeIconClass(string? source, string? linkUrl, string? currentIconClass)
    {
        if (!string.IsNullOrWhiteSpace(linkUrl))
        {
            if (linkUrl.Contains("/Integration/DocumentSigning", StringComparison.OrdinalIgnoreCase)
                || linkUrl.Contains("/DocumentSigning/", StringComparison.OrdinalIgnoreCase))
            {
                return "fas fa-file-signature";
            }

            if (linkUrl.Contains("/Integration/FlowEngine", StringComparison.OrdinalIgnoreCase))
            {
                return "fa fa-bolt";
            }

            if (linkUrl.Contains("/ExcelImport", StringComparison.OrdinalIgnoreCase))
            {
                return "fas fa-file-excel";
            }

            if (linkUrl.Contains("/Invoices", StringComparison.OrdinalIgnoreCase))
            {
                return "fas fa-file-invoice";
            }

            if (linkUrl.Contains("/Orders", StringComparison.OrdinalIgnoreCase))
            {
                return "fas fa-table-list";
            }

            if (linkUrl.Contains("/NotifyMe", StringComparison.OrdinalIgnoreCase))
            {
                return "fas fa-broadcast-tower";
            }

            if (linkUrl.Contains("/Support", StringComparison.OrdinalIgnoreCase))
            {
                return "fa fa-life-ring";
            }
        }

        return source?.Trim() switch
        {
            "Oneflow" => "fas fa-file-signature",
            "Jeeves" => "fa fa-bolt",
            "Centra" => "fa fa-bolt",
            "Shopify" => "fa fa-bolt",
            "Akeneo" => "fa fa-bolt",
            "NotifyMe" => "fas fa-broadcast-tower",
            "ExcelImport" => "fas fa-file-excel",
            "Support" => "fa fa-life-ring",
            _ => string.IsNullOrWhiteSpace(currentIconClass) ? "fa fa-circle" : currentIconClass
        };
    }

    private static IEnumerable<SidebarRuntimeStatusItemViewModel> CollapseItems(IEnumerable<SidebarRuntimeStatusItemViewModel> items)
    {
        return items
            .GroupBy(item => string.IsNullOrWhiteSpace(item.AggregateKey)
                ? $"{item.OccurredAtUtc:O}|{item.Source}|{item.Title}|{item.StatusLabel}"
                : item.AggregateKey!)
            .Select(group => group
                .OrderByDescending(item => item.OccurredAtUtc)
                .First());
    }

    private static bool IsActiveItem(SidebarRuntimeStatusItemViewModel item)
        => string.Equals(item.StatusLabel, "Running", StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.StatusLabel, "Queued", StringComparison.OrdinalIgnoreCase);

    private string ToDisplayTime(DateTimeOffset when, string? statusTone, string? statusLabel)
    {
        if (string.Equals(statusTone, "info", StringComparison.OrdinalIgnoreCase))
        {
            var elapsed = DateTimeOffset.UtcNow - when.ToUniversalTime();
            var wholeMinutes = Math.Max(0, (int)elapsed.TotalMinutes);
            return wholeMinutes <= 0 ? _sharedLocalizer["SidebarRuntime_TimeRunning"].Value : $"{wholeMinutes} min";
        }

        return ToRelativeTime(when);
    }

    private string ToRelativeTime(DateTimeOffset when)
    {
        var delta = DateTimeOffset.UtcNow - when.ToUniversalTime();
        if (delta.TotalSeconds < 60)
            return _sharedLocalizer["SidebarRuntime_TimeNow"].Value;
        if (delta.TotalMinutes < 60)
            return $"{Math.Max(1, (int)delta.TotalMinutes)} min";
        if (delta.TotalHours < 24)
            return $"{Math.Max(1, (int)delta.TotalHours)} h";
        return $"{Math.Max(1, (int)delta.TotalDays)} d";
    }

    private string? BuildFlowEngineLink(FlowEngineJobSnapshot job)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return null;

        var action = FlowEngineJobPresentation.GetSystemLabel(job) switch
        {
            "Jeeves" => nameof(IntegrationController.FlowEngineJeeves),
            "Centra" => nameof(IntegrationController.FlowEngineCentra),
            "Shopify" => nameof(IntegrationController.FlowEngineShopify),
            "Akeneo" => nameof(IntegrationController.FlowEngineAkeneo),
            _ => nameof(IntegrationController.FlowEngine)
        };

        return _linkGenerator.GetPathByAction(
            httpContext,
            action,
            "Integration",
            new { selectedJobId = job.Id });
    }

    private DateTimeOffset GetLastReadAt(Guid companyId)
    {
        var sessionUser = _httpContextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
        var userId = sessionUser?.UserId;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            try
            {
                using var db = _dbContextFactory.CreateDbContext();
                var persisted = db.SidebarRuntimeNotificationReadStates!
                    .AsNoTracking()
                    .Where(item => item.CompanyId == companyId && item.UserId == userId)
                    .Select(item => (DateTime?)item.LastReadAtUtc)
                    .SingleOrDefault();

                if (persisted.HasValue)
                {
                    return new DateTimeOffset(DateTime.SpecifyKind(persisted.Value, DateTimeKind.Utc));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load persisted sidebar notification read state. Falling back to session.");
            }
        }

        var session = _httpContextAccessor.HttpContext?.Session;
        if (session is null)
            return DateTimeOffset.MinValue;

        var raw = session.GetString(GetReadAtKey(companyId, userId));
        return DateTimeOffset.TryParse(raw, out var parsed) ? parsed : DateTimeOffset.MinValue;
    }

    private static string GetReadAtKey(Guid companyId, string? userId)
        => $"{ReadAtSessionKeyPrefix}.{userId ?? "anonymous"}.{companyId:N}";
}

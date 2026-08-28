using LoggerService;
using Microsoft.Data.SqlClient;
using WebApp.Repositories.NotifyMe;
using WebApp.Services.Integration;
using WebApp.ViewModels.NotifyMe;

namespace WebApp.Services.NotifyMe;

// Composes read-only NotifyMe pages without owning editor, save, or execution flows.
public sealed class NotifyMePageQueryService : INotifyMePageQueryService
{
    private readonly INotifyMeRepository _repository;
    private readonly ILoggerManager _logger;

    public NotifyMePageQueryService(INotifyMeRepository repository, ILoggerManager logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<NotifyMeOverviewVm> GetOverviewAsync(
        string? connectionString,
        int? companyCode,
        string? search = null,
        string? status = null,
        string? type = null,
        string? priority = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (!NotifyMeConnectionContext.HasConnectionContext(connectionString, companyCode, out var message))
            return UnavailableOverview(companyCode, message);

        try
        {
            var notifications = await _repository.GetNotificationsAsync(connectionString!, companyCode!.Value, cancellationToken);
            var filteredNotifications = ApplyNotificationFilters(notifications, search, status, type, priority);
            var pagedNotifications = Paginate(filteredNotifications, page, 10, out var pagination);

            var typeOptions = notifications
                .Select(x => x.TypeLabel)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .Select(x => new NotifyMeLookupOptionVm { Value = x, Label = x })
                .ToArray();

            var priorityOptions = notifications
                .Select(x => x.PriorityLabel)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .Select(x => new NotifyMeLookupOptionVm { Value = x, Label = x })
                .ToArray();

            var historyNotificationOptions = BuildHistoryNotificationOptions(notifications);
            var recentLogEntries = await _repository.GetRecentLogEntriesAsync(connectionString!, companyCode.Value, take: 6, cancellationToken: cancellationToken);

            return new NotifyMeOverviewVm
            {
                IsInstalled = true,
                CompanyCode = companyCode,
                StatusMessage = notifications.Count == 0 ? "Inga notifieringar hittades för valt bolag ännu." : null,
                Notifications = pagedNotifications,
                RecentLogEntries = recentLogEntries,
                TotalNotifications = notifications.Count,
                ActiveNotifications = notifications.Count(x => x.IsActive),
                DueNowCount = notifications.Count(x => x.IsDueNow),
                EscalationConfiguredCount = notifications.Count(x => x.EscalateAfterCount.HasValue && x.EscalateAfterCount.Value > 0),
                FilteredNotificationsCount = filteredNotifications.Count,
                Pagination = pagination,
                Filters = new NotifyMeOverviewFiltersVm
                {
                    Search = search,
                    Status = string.IsNullOrWhiteSpace(status) ? "all" : status,
                    Type = type,
                    Priority = priority,
                    TypeOptions = typeOptions,
                    PriorityOptions = priorityOptions,
                    HistoryNotificationOptions = historyNotificationOptions
                }
            };
        }
        catch (SqlException ex) when (ex.Number is 208 or 207 or 2812)
        {
            _logger.LogWarning($"NotifyMe unavailable for company {companyCode}: {IntegrationLogSanitizer.Diagnostic(ex.Message)}");
            return UnavailableOverview(companyCode, "NotifyMe verkar inte vara installerat i den här Jeeves-databasen ännu.");
        }
    }

    public async Task<NotifyMeHistoryPageVm> GetHistoryAsync(
        string? connectionString,
        int? companyCode,
        int? historyNotificationId = null,
        string? historySearch = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (!NotifyMeConnectionContext.HasConnectionContext(connectionString, companyCode, out var message))
            return UnavailableHistory(companyCode, message);

        try
        {
            var notifications = await _repository.GetNotificationsAsync(connectionString!, companyCode!.Value, cancellationToken);
            var allHistoryEntries = await _repository.GetRecentLogEntriesAsync(connectionString!, companyCode.Value, take: 200, cancellationToken: cancellationToken);
            var filteredHistory = ApplyHistoryFilters(allHistoryEntries, historyNotificationId, historySearch);
            var pagedHistory = Paginate(filteredHistory, page, 15, out var pagination);

            return new NotifyMeHistoryPageVm
            {
                IsInstalled = true,
                CompanyCode = companyCode,
                StatusMessage = filteredHistory.Count == 0 ? "Ingen historik matchar aktuellt urval." : null,
                TotalHistoryEntries = filteredHistory.Count,
                HistoryEntries = pagedHistory,
                Pagination = pagination,
                Filters = new NotifyMeOverviewFiltersVm
                {
                    HistoryNotificationId = historyNotificationId,
                    HistorySearch = historySearch,
                    HistoryNotificationOptions = BuildHistoryNotificationOptions(notifications)
                }
            };
        }
        catch (SqlException ex) when (ex.Number is 208 or 207 or 2812)
        {
            _logger.LogWarning($"NotifyMe history unavailable for company {companyCode}: {IntegrationLogSanitizer.Diagnostic(ex.Message)}");
            return UnavailableHistory(companyCode, "NotifyMe verkar inte vara installerat i den här Jeeves-databasen ännu.");
        }
    }

    public async Task<NotifyMeTemplateLibraryVm> GetTemplateLibraryAsync(
        string? connectionString,
        int? companyCode,
        string? search = null,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        if (!NotifyMeConnectionContext.HasConnectionContext(connectionString, companyCode, out var message))
            return UnavailableTemplateLibrary(companyCode, search, category, message);

        try
        {
            var notifications = await _repository.GetNotificationsAsync(connectionString!, companyCode!.Value, cancellationToken);
            var templates = notifications.Select(MapTemplate).ToArray();
            var filteredTemplates = ApplyTemplateFilters(templates, search, category);

            var categoryOptions = templates
                .Select(x => x.Category)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .Select(x => new NotifyMeLookupOptionVm { Value = x, Label = x })
                .ToArray();

            return new NotifyMeTemplateLibraryVm
            {
                IsInstalled = true,
                CompanyCode = companyCode,
                Search = search,
                Category = category,
                TotalTemplates = filteredTemplates.Count,
                CategoryOptions = categoryOptions,
                Templates = filteredTemplates
            };
        }
        catch (SqlException ex) when (ex.Number is 208 or 207 or 2812)
        {
            _logger.LogWarning($"NotifyMe template library unavailable for company {companyCode}: {IntegrationLogSanitizer.Diagnostic(ex.Message)}");
            return UnavailableTemplateLibrary(companyCode, search, category, "NotifyMe-tabellerna finns inte i den här Jeeves-databasen ännu.");
        }
    }

    public async Task<NotifyMeStatisticsVm> GetStatisticsAsync(
        string? connectionString,
        int? companyCode,
        CancellationToken cancellationToken = default)
    {
        if (!NotifyMeConnectionContext.HasConnectionContext(connectionString, companyCode, out var message))
            return UnavailableStatistics(companyCode, message);

        try
        {
            var notifications = await _repository.GetNotificationsAsync(connectionString!, companyCode!.Value, cancellationToken);
            var allLogs = await _repository.GetRecentLogEntriesAsync(connectionString!, companyCode.Value, take: 500, cancellationToken: cancellationToken);

            var latestLogDate = allLogs
                .Where(x => x.SentAt.HasValue)
                .Select(x => x.SentAt!.Value.Date)
                .DefaultIfEmpty(DateTime.Today)
                .Max();

            var rollingPeriodStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
            var hasRecentActivity = allLogs.Any(x => x.SentAt.HasValue && x.SentAt.Value.Date >= rollingPeriodStart);

            var periodEnd = hasRecentActivity ? DateTime.Today : latestLogDate;
            var periodStart = new DateTime(periodEnd.Year, periodEnd.Month, 1).AddMonths(-5);
            var periodLogs = allLogs
                .Where(x => x.SentAt.HasValue && x.SentAt.Value.Date >= periodStart && x.SentAt.Value.Date <= periodEnd)
                .ToList();

            var activeNotifications = notifications.Where(x => x.IsActive).ToList();
            var logsByNotification = periodLogs
                .GroupBy(x => x.NotificationId)
                .ToDictionary(g => g.Key, g => g.Count());

            var trend = Enumerable.Range(0, 6)
                .Select(offset => periodStart.AddMonths(offset))
                .Select(monthStart =>
                {
                    var monthEnd = monthStart.AddMonths(1);
                    var monthLogs = periodLogs
                        .Where(x => x.SentAt.HasValue && x.SentAt.Value >= monthStart && x.SentAt.Value < monthEnd)
                        .ToList();

                    return new NotifyMeStatsPointVm
                    {
                        Label = monthStart.ToString("MMM", new System.Globalization.CultureInfo("sv-SE")).Trim('.'),
                        RunCount = monthLogs.Count,
                        HitCount = monthLogs.Select(x => x.NotificationId).Distinct().Count()
                    };
                })
                .ToList();

            var maxRuns = Math.Max(1, trend.Max(x => x.RunCount));
            foreach (var point in trend)
                point.HeightPercent = Math.Max(14, (int)Math.Round(point.RunCount / (double)maxRuns * 100d));

            var notificationRows = notifications
                .OrderByDescending(x => logsByNotification.GetValueOrDefault(x.NotificationId))
                .ThenByDescending(x => x.WarningCount)
                .ThenBy(x => x.NotificationId)
                .Select(x =>
                {
                    var recentHits = logsByNotification.GetValueOrDefault(x.NotificationId);
                    var share = periodLogs.Count == 0 ? 0m : Math.Round(recentHits * 100m / periodLogs.Count, 1);
                    var (qualityLabel, qualityTone) = ClassifyStatisticsRow(x.IsActive, recentHits);

                    return new NotifyMeNotificationStatsRowVm
                    {
                        NotificationId = x.NotificationId,
                        Description = x.Description,
                        Category = x.TypeLabel,
                        RunCount = x.WarningCount,
                        HitCount = recentHits,
                        HitRatePercent = share,
                        QualityLabel = qualityLabel,
                        QualityTone = qualityTone
                    };
                })
                .ToArray();

            var activeWithHits = activeNotifications.Count == 0
                ? 0
                : activeNotifications.Count(x => logsByNotification.ContainsKey(x.NotificationId));

            var insights = BuildStatisticsInsights(notifications, activeNotifications, periodLogs.Count, activeWithHits, notificationRows);

            return new NotifyMeStatisticsVm
            {
                IsInstalled = true,
                CompanyCode = companyCode,
                PeriodLabel = hasRecentActivity
                    ? $"Senaste 6 månaderna ({periodStart:yyyy-MM-dd} - {periodEnd:yyyy-MM-dd})"
                    : $"Senaste 6 månaderna med aktivitet ({periodStart:yyyy-MM-dd} - {periodEnd:yyyy-MM-dd})",
                TotalRuns = periodLogs.Count,
                TotalHits = activeWithHits,
                HitRatePercent = activeNotifications.Count == 0 ? 0m : Math.Round(activeWithHits * 100m / activeNotifications.Count, 1),
                EstimatedHoursSaved = activeNotifications.Count == 0 ? 0m : Math.Round(periodLogs.Count / (decimal)activeNotifications.Count, 1),
                EstimatedValueProtectedSek = notifications.Sum(x => x.WarningCount),
                Trend = trend,
                NotificationRows = notificationRows,
                Insights = insights
            };
        }
        catch (SqlException ex) when (ex.Number is 208 or 207 or 2812)
        {
            _logger.LogWarning($"NotifyMe statistics unavailable for company {companyCode}: {IntegrationLogSanitizer.Diagnostic(ex.Message)}");
            return UnavailableStatistics(companyCode, "NotifyMe-statistik kunde inte läsas från den här Jeeves-databasen ännu.");
        }
    }

    public async Task<NotifyMeDetailsPageVm> GetDetailsAsync(string? connectionString, int? companyCode, int notificationId, CancellationToken cancellationToken = default)
    {
        if (!NotifyMeConnectionContext.HasConnectionContext(connectionString, companyCode, out var message))
            return UnavailableDetails(companyCode, message);

        try
        {
            var notification = await _repository.GetNotificationAsync(connectionString!, companyCode!.Value, notificationId, cancellationToken);
            if (notification != null)
            {
                notification.RecentLogEntries = await _repository.GetRecentLogEntriesAsync(
                    connectionString!,
                    companyCode.Value,
                    notification.NotificationId,
                    take: 12,
                    cancellationToken: cancellationToken);
            }

            return new NotifyMeDetailsPageVm
            {
                IsInstalled = true,
                CompanyCode = companyCode,
                StatusMessage = notification == null ? "Notifieringen hittades inte för valt bolag." : null,
                Notification = notification
            };
        }
        catch (SqlException ex) when (ex.Number is 208 or 207 or 2812)
        {
            _logger.LogWarning($"NotifyMe details unavailable for company {companyCode}: {IntegrationLogSanitizer.Diagnostic(ex.Message)}");
            return UnavailableDetails(companyCode, "NotifyMe verkar inte vara installerat i den här Jeeves-databasen ännu.");
        }
    }

    private static NotifyMeOverviewVm UnavailableOverview(int? companyCode, string message)
    {
        return new NotifyMeOverviewVm
        {
            IsInstalled = false,
            CompanyCode = companyCode,
            StatusMessage = message
        };
    }

    private static NotifyMeDetailsPageVm UnavailableDetails(int? companyCode, string message)
    {
        return new NotifyMeDetailsPageVm
        {
            IsInstalled = false,
            CompanyCode = companyCode,
            StatusMessage = message
        };
    }

    private static NotifyMeHistoryPageVm UnavailableHistory(int? companyCode, string message)
    {
        return new NotifyMeHistoryPageVm
        {
            IsInstalled = false,
            CompanyCode = companyCode,
            StatusMessage = message
        };
    }

    private static NotifyMeTemplateLibraryVm UnavailableTemplateLibrary(int? companyCode, string? search, string? category, string message)
    {
        return new NotifyMeTemplateLibraryVm
        {
            IsInstalled = false,
            StatusMessage = message,
            CompanyCode = companyCode,
            Search = search,
            Category = category
        };
    }

    private static NotifyMeStatisticsVm UnavailableStatistics(int? companyCode, string message)
    {
        return new NotifyMeStatisticsVm
        {
            IsInstalled = false,
            StatusMessage = message,
            CompanyCode = companyCode,
            PeriodLabel = "Senaste 6 månaderna",
            Insights = new[]
            {
                new NotifyMeStatsInsightVm
                {
                    Title = "Statistik saknas",
                    Description = message,
                    Tone = "warning"
                }
            }
        };
    }

    private static NotifyMeTemplateVm MapTemplate(NotifyMeListItemVm notification)
    {
        return new NotifyMeTemplateVm
        {
            Key = notification.NotificationId.ToString(),
            SourceNotificationId = notification.NotificationId,
            Title = string.IsNullOrWhiteSpace(notification.Description)
                ? $"NotifyMe {notification.NotificationId}"
                : notification.Description,
            Category = string.IsNullOrWhiteSpace(notification.TypeLabel) ? "Okategoriserad" : notification.TypeLabel,
            Summary = string.IsNullOrWhiteSpace(notification.WarningText)
                ? "Notifiering från aktivt bolags NotifyMe-konfiguration."
                : notification.WarningText,
            BusinessValue = notification.IsActive
                ? "Aktiv notifiering som ingår i bolagets nuvarande NotifyMe-flöde."
                : "Inaktiv notifiering som kan granskas och återaktiveras vid behov.",
            ExampleFrequency = string.IsNullOrWhiteSpace(notification.ScheduleLabel) ? "-" : notification.ScheduleLabel,
            SuggestedPriority = string.IsNullOrWhiteSpace(notification.PriorityLabel) ? "-" : notification.PriorityLabel,
            ComplexityLabel = notification.HasAutomation ? "Automatiserad" : "Manuell",
            ParameterHints = BuildTemplateHints(notification)
        };
    }

    private static IReadOnlyList<NotifyMeListItemVm> ApplyNotificationFilters(
        IReadOnlyList<NotifyMeListItemVm> notifications,
        string? search,
        string? status,
        string? type,
        string? priority)
    {
        var query = notifications.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.WarningText.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.NotificationId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        switch (status?.Trim().ToLowerInvariant())
        {
            case "active":
                query = query.Where(x => x.IsActive);
                break;
            case "inactive":
                query = query.Where(x => !x.IsActive);
                break;
            case "due":
                query = query.Where(x => x.IsDueNow);
                break;
        }

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(x => string.Equals(x.TypeLabel, type, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(priority))
            query = query.Where(x => string.Equals(x.PriorityLabel, priority, StringComparison.OrdinalIgnoreCase));

        return query.ToArray();
    }

    private static IReadOnlyList<NotifyMeLogItemVm> ApplyHistoryFilters(
        IReadOnlyList<NotifyMeLogItemVm> logs,
        int? historyNotificationId,
        string? historySearch)
    {
        var query = logs.AsEnumerable();

        if (historyNotificationId.HasValue)
            query = query.Where(x => x.NotificationId == historyNotificationId.Value);

        if (!string.IsNullOrWhiteSpace(historySearch))
        {
            query = query.Where(x =>
                x.Subject.Contains(historySearch, StringComparison.OrdinalIgnoreCase) ||
                x.Recipients.Contains(historySearch, StringComparison.OrdinalIgnoreCase) ||
                x.NotificationDescription.Contains(historySearch, StringComparison.OrdinalIgnoreCase) ||
                x.HtmlPreviewText.Contains(historySearch, StringComparison.OrdinalIgnoreCase));
        }

        return query.ToArray();
    }

    private static IReadOnlyList<NotifyMeTemplateVm> ApplyTemplateFilters(
        IReadOnlyList<NotifyMeTemplateVm> templates,
        string? search,
        string? category)
    {
        var query = templates.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Key.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Summary.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase));

        return query
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Title)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildTemplateHints(NotifyMeListItemVm notification)
    {
        var hints = new List<string>
        {
            notification.IsActive ? "Aktiv" : "Inaktiv"
        };

        if (notification.IsDueNow)
            hints.Add("Ska köras");

        if (notification.WarningCount > 0)
            hints.Add($"{notification.WarningCount} varningar");

        if (notification.EscalateAfterCount.GetValueOrDefault() > 0)
            hints.Add("Eskalering");

        return hints;
    }

    private static IReadOnlyList<NotifyMeLookupOptionVm> BuildHistoryNotificationOptions(IReadOnlyList<NotifyMeListItemVm> notifications)
    {
        return notifications
            .OrderBy(x => x.NotificationId)
            .Select(x => new NotifyMeLookupOptionVm
            {
                Value = x.NotificationId.ToString(),
                Label = $"{x.NotificationId} - {x.Description}"
            })
            .ToArray();
    }

    private static IReadOnlyList<T> Paginate<T>(IReadOnlyList<T> items, int page, int pageSize, out NotifyMePaginationVm pagination)
    {
        var safePageSize = pageSize <= 0 ? 10 : pageSize;
        var totalItems = items.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)safePageSize));
        var safePage = Math.Min(Math.Max(page, 1), totalPages);

        pagination = new NotifyMePaginationVm
        {
            Page = safePage,
            PageSize = safePageSize,
            TotalItems = totalItems
        };

        return items
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToArray();
    }

    private static (string Label, string Tone) ClassifyStatisticsRow(bool isActive, int recentHits)
    {
        if (!isActive)
            return ("Inaktiv", "secondary");

        if (recentHits >= 10)
            return ("Hög aktivitet", "success");

        if (recentHits >= 1)
            return ("Måttlig aktivitet", "warning");

        return ("Ingen aktivitet", "danger");
    }

    private static IReadOnlyList<NotifyMeStatsInsightVm> BuildStatisticsInsights(
        IReadOnlyList<NotifyMeListItemVm> notifications,
        IReadOnlyList<NotifyMeListItemVm> activeNotifications,
        int periodRunCount,
        int activeWithHits,
        IReadOnlyList<NotifyMeNotificationStatsRowVm> rows)
    {
        var insights = new List<NotifyMeStatsInsightVm>();

        var busiest = rows.FirstOrDefault(x => x.HitCount > 0);
        if (busiest != null)
        {
            insights.Add(new NotifyMeStatsInsightVm
            {
                Title = "Mest aktiv notifiering",
                Description = $"{busiest.Description} stod för {busiest.HitCount} utskick under perioden.",
                Tone = "info"
            });
        }

        var silentActive = activeNotifications.Count - activeWithHits;
        if (silentActive > 0)
        {
            insights.Add(new NotifyMeStatsInsightVm
            {
                Title = "Aktiva utan utskick",
                Description = $"{silentActive} aktiva notifieringar har inte gett några utskick under perioden och kan vara kandidater för justering eller städning.",
                Tone = "warning"
            });
        }

        insights.Add(new NotifyMeStatsInsightVm
        {
            Title = "Samlad aktivitet",
            Description = $"Totalt {periodRunCount} utskick har loggats under perioden för {notifications.Count} notifieringar.",
            Tone = "success"
        });

        return insights;
    }
}

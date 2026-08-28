using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Entities.Application;
using Microsoft.Data.SqlClient;
using WebApp.Models.ActionCenter;
using WebApp.Repositories.NotifyMe;
using WebApp.Services.Application;
using WebApp.ViewModels.NotifyMe;

namespace WebApp.Services.ActionCenter;

/// <summary>
/// Surfaces NotifyMe activity that requires attention without forcing users into the NotifyMe module first.
/// </summary>
public sealed class NotifyMeInsightProvider : IInsightProvider
{
    private const int IndividualInsightThreshold = 3;

    private readonly INotifyMeRepository _notifyMeRepository;

    public string ProviderKey => "customer-notifyme";
    public ActionCenterAudience Audience => ActionCenterAudience.Customer;

    public NotifyMeInsightProvider(INotifyMeRepository notifyMeRepository)
    {
        _notifyMeRepository = notifyMeRepository;
    }

    public async Task<IEnumerable<ActionCenterInsight>> GetInsightsAsync(UserSession user, JeevesRuntimeContext? runtimeContext, CancellationToken cancellationToken)
    {
        var connectionString = runtimeContext?.ConnectionString ?? string.Empty;
        var companyCode = runtimeContext?.CompanyCode ?? user.JeevesActiveCompany;

        if (string.IsNullOrWhiteSpace(connectionString) || companyCode == null)
            return Array.Empty<ActionCenterInsight>();

        try
        {
            var notifications = await _notifyMeRepository.GetNotificationsAsync(connectionString, companyCode.Value, cancellationToken);
            var historyEntries = await _notifyMeRepository.GetRecentLogEntriesAsync(connectionString, companyCode.Value, take: 200, cancellationToken: cancellationToken);
            if (notifications.Count == 0)
                return Array.Empty<ActionCenterInsight>();

            var insights = new List<ActionCenterInsight>();
            var latestLogsByNotification = historyEntries
                .Where(x => x.SentAt.HasValue)
                .GroupBy(x => x.NotificationId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.SentAt).First());

            var dueNow = notifications
                .Where(x => x.IsActive && x.IsDueNow)
                .OrderBy(x => x.NextExecutionAt ?? DateTime.MaxValue)
                .ToList();

            if (dueNow.Count > 0)
            {
                if (dueNow.Count <= IndividualInsightThreshold)
                {
                    insights.AddRange(dueNow.Select(BuildDueNowInsight));
                }
                else
                {
                    var oldestDue = dueNow.First().NextExecutionAt ?? DateTime.UtcNow;
                    insights.Add(new ActionCenterInsight
                    {
                        Key = "notifyme-due-now",
                        Audience = ActionCenterAudience.Customer,
                        Category = "NotifyMe",
                        Status = ActionCenterStatus.Open,
                        Title = $"{dueNow.Count} NotifyMe-regler förfaller nu",
                        Description = "Aktiva notifieringar har nått eller passerat nästa körning. Kontrollera schema, mottagare och att reglerna ger rätt träffbild.",
                        Priority = ActionCenterPriority.High,
                        DetectedAt = oldestDue,
                        DueAt = oldestDue,
                        LinkText = "Öppna NotifyMe",
                        LinkUrl = "/NotifyMe"
                    });
                }
            }

            var manualActionNotifications = notifications
                .Where(x => x.IsActive)
                .Where(x => latestLogsByNotification.TryGetValue(x.NotificationId, out var latestLog)
                    && string.Equals(latestLog.ExecutionStatus, "Manuell åtgärd", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => latestLogsByNotification[x.NotificationId].SentAt ?? DateTime.MaxValue)
                .ToList();

            if (manualActionNotifications.Count > 0)
            {
                if (manualActionNotifications.Count <= IndividualInsightThreshold)
                {
                    insights.AddRange(manualActionNotifications.Select(notification =>
                        BuildManualActionInsight(notification, latestLogsByNotification[notification.NotificationId])));
                }
                else
                {
                    var firstManualAction = manualActionNotifications[0];
                    var firstManualActionLog = latestLogsByNotification[firstManualAction.NotificationId];
                    insights.Add(new ActionCenterInsight
                    {
                        Key = "notifyme-manual-action",
                        Audience = ActionCenterAudience.Customer,
                        Category = "NotifyMe",
                        Status = ActionCenterStatus.Open,
                        Title = $"{manualActionNotifications.Count} NotifyMe-regler kräver manuell åtgärd",
                        Description = "Automatiska retries är uttömda. Kontrollera schema, mottagare, datakälla och SQL innan notifieringen aktiveras fullt igen.",
                        Priority = ActionCenterPriority.High,
                        DetectedAt = firstManualActionLog.SentAt ?? DateTime.UtcNow,
                        DueAt = firstManualActionLog.SentAt,
                        LinkText = "Öppna NotifyMe",
                        LinkUrl = "/NotifyMe"
                    });
                }
            }

            var highWarningWithoutRecentExecution = notifications
                .Where(x => x.IsActive)
                .Where(x => x.WarningCount >= 10)
                .Where(x => !x.LastWarningAt.HasValue || x.LastWarningAt.Value < DateTime.Today.AddDays(-30))
                .OrderByDescending(x => x.WarningCount)
                .FirstOrDefault();

            if (highWarningWithoutRecentExecution != null)
            {
                insights.Add(new ActionCenterInsight
                {
                    Key = $"notifyme-stale-high-warning-{highWarningWithoutRecentExecution.NotificationId}",
                    Audience = ActionCenterAudience.Customer,
                    Category = "NotifyMe",
                    Status = ActionCenterStatus.Open,
                    Title = $"Notifiering {highWarningWithoutRecentExecution.NotificationId} bör ses över",
                    Description = $"{highWarningWithoutRecentExecution.Description} har {highWarningWithoutRecentExecution.WarningCount:N0} varningar totalt men ingen färsk träff senaste 30 dagarna. Regeln kan behöva justeras eller städas.",
                    Priority = ActionCenterPriority.Low,
                    DetectedAt = highWarningWithoutRecentExecution.LastWarningAt ?? DateTime.UtcNow,
                    LinkText = "Se notifiering",
                    LinkUrl = $"/NotifyMe/Detail/{highWarningWithoutRecentExecution.NotificationId}"
                });
            }

            return insights;
        }
        catch (SqlException ex) when (ex.Number is 208 or 207 or 2812)
        {
            return Array.Empty<ActionCenterInsight>();
        }
    }

    private static ActionCenterInsight BuildDueNowInsight(NotifyMeListItemVm notification)
    {
        var dueAt = notification.NextExecutionAt ?? DateTime.UtcNow;

        return new ActionCenterInsight
        {
            Key = $"notifyme-due-now-{notification.NotificationId}",
            Audience = ActionCenterAudience.Customer,
            Category = "NotifyMe",
            Status = ActionCenterStatus.Open,
            Title = $"NotifyMe {notification.NotificationId} förfaller nu",
            Description = $"{FormatNotificationName(notification)} har nått eller passerat nästa körning. Kontrollera schema, mottagare och träffbild innan regeln lämnas utan åtgärd.",
            Priority = ActionCenterPriority.Medium,
            DetectedAt = dueAt,
            DueAt = dueAt,
            LinkText = "Öppna notifiering",
            LinkUrl = $"/NotifyMe/Detail/{notification.NotificationId}",
            Metrics = BuildNotificationMetrics(notification)
        };
    }

    private static ActionCenterInsight BuildManualActionInsight(NotifyMeListItemVm notification, NotifyMeLogItemVm latestLog)
    {
        var detectedAt = latestLog.SentAt ?? DateTime.UtcNow;

        return new ActionCenterInsight
        {
            Key = $"notifyme-manual-action-{notification.NotificationId}",
            Audience = ActionCenterAudience.Customer,
            Category = "NotifyMe",
            Status = ActionCenterStatus.Open,
            Title = $"NotifyMe {notification.NotificationId} kräver manuell åtgärd",
            Description = $"{FormatNotificationName(notification)} har senaste status \"{latestLog.ExecutionStatus}\". Kontrollera schema, mottagare, datakälla och SQL innan notifieringen aktiveras fullt igen.",
            Priority = ActionCenterPriority.High,
            DetectedAt = detectedAt,
            DueAt = detectedAt,
            LinkText = "Öppna notifiering",
            LinkUrl = $"/NotifyMe/Detail/{notification.NotificationId}",
            Metrics = BuildNotificationMetrics(notification)
        };
    }

    private static IReadOnlyList<ActionCenterMetric> BuildNotificationMetrics(NotifyMeListItemVm notification)
    {
        var metrics = new List<ActionCenterMetric>
        {
            new()
            {
                Label = "Varningar",
                Value = notification.WarningCount.ToString("N0")
            }
        };

        if (!string.IsNullOrWhiteSpace(notification.ScheduleLabel))
        {
            metrics.Add(new ActionCenterMetric
            {
                Label = "Schema",
                Value = notification.ScheduleLabel
            });
        }

        if (!string.IsNullOrWhiteSpace(notification.TypeLabel))
        {
            metrics.Add(new ActionCenterMetric
            {
                Label = "Typ",
                Value = notification.TypeLabel
            });
        }

        if (!string.IsNullOrWhiteSpace(notification.LatestExecutionStatus))
        {
            metrics.Add(new ActionCenterMetric
            {
                Label = "Senaste status",
                Value = notification.LatestExecutionStatus
            });
        }

        return metrics;
    }

    private static string FormatNotificationName(NotifyMeListItemVm notification)
        => string.IsNullOrWhiteSpace(notification.Description)
            ? $"NotifyMe {notification.NotificationId}"
            : notification.Description.Trim();
}

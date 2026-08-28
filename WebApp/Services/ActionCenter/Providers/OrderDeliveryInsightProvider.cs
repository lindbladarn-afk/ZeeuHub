using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Entities.Application;
using WebApp.Models.ActionCenter;
using WebApp.Models.Orders;
using WebApp.Repositories.Orders;
using WebApp.Services.Application;

namespace WebApp.Services.ActionCenter;

/// <summary>
/// Builds operational order insights from real Jeeves delivery dates.
/// Focuses on the two most actionable cases: overdue deliveries and future backlog.
/// </summary>
public sealed class OrderDeliveryInsightProvider : IInsightProvider
{
    private const int FutureTimelineMonths = 6;
    private readonly IOrdersRepository _ordersRepository;

    public string ProviderKey => "customer-order-delivery";
    public ActionCenterAudience Audience => ActionCenterAudience.Customer;

    public OrderDeliveryInsightProvider(IOrdersRepository ordersRepository)
    {
        _ordersRepository = ordersRepository;
    }

    public async Task<IEnumerable<ActionCenterInsight>> GetInsightsAsync(UserSession user, JeevesRuntimeContext? runtimeContext, CancellationToken cancellationToken)
    {
        var connectionString = runtimeContext?.ConnectionString ?? string.Empty;
        var companyCode = runtimeContext?.CompanyCode ?? user.JeevesActiveCompany;

        if (string.IsNullOrWhiteSpace(connectionString) || companyCode == null)
            return Array.Empty<ActionCenterInsight>();

        var overdueQuery = new GetOrderDeliveryInsightQuery
        {
            CompanyCode = companyCode
        };
        var futureQuery = new GetDeliveryForecastQuery
        {
            CompanyCode = companyCode,
            MonthsAhead = FutureTimelineMonths
        };

        var overdue = await _ordersRepository.GetOverdueDeliverySummaryAsync(connectionString, overdueQuery);
        var future = await _ordersRepository.GetFutureDeliverySummaryAsync(connectionString, futureQuery);

        var insights = new List<ActionCenterInsight>();

        if (overdue.OrderCount > 0)
        {
            var oldestDate = overdue.EarliestDate?.ToString("yyyy-MM-dd") ?? "okänt datum";
            insights.Add(new ActionCenterInsight
            {
                Key = "orders-overdue-delivery",
                Audience = ActionCenterAudience.Customer,
                Category = "Orders",
                Status = ActionCenterStatus.Open,
                Title = overdue.OrderCount == 1
                    ? "1 order har passerat lovad leverans"
                    : $"{overdue.OrderCount} orders har passerat lovad leverans",
                Description = $"{overdue.OrderCount} öppna orders ligger efter planerad/lovad leverans · {overdue.AmountTotal:N0} kr. Äldsta datum: {oldestDate}.",
                Priority = overdue.OrderCount >= 5 ? ActionCenterPriority.High : ActionCenterPriority.Medium,
                DetectedAt = overdue.EarliestDate ?? DateTime.UtcNow,
                DueAt = overdue.EarliestDate,
                LinkText = "Öppna orders",
                LinkUrl = "/Orders/Index"
            });
        }

        if (future.OrderCount > 0)
        {
            var latestDate = future.LatestDate?.ToString("yyyy-MM-dd") ?? "okänt datum";
            var culture = CultureInfo.GetCultureInfo("sv-SE");
            var timeline = await _ordersRepository.GetFutureDeliveryTimelineAsync(connectionString, futureQuery);
            var topMonth = timeline
                .OrderByDescending(x => x.OrderCount)
                .ThenByDescending(x => x.AmountTotal)
                .FirstOrDefault();

            insights.Add(new ActionCenterInsight
            {
                Key = "orders-future-delivery",
                Audience = ActionCenterAudience.Customer,
                Category = "Orders",
                Status = ActionCenterStatus.Open,
                Title = future.OrderCount == 1
                    ? "1 order ligger framåt i leveransplan"
                    : $"{future.OrderCount} orders ligger framåt i leveransplan",
                Description = $"{future.OrderCount} öppna orders har leveransdatum framåt i tiden · {future.AmountTotal:N0} kr. Senaste planerade leverans: {latestDate}.",
                Priority = ActionCenterPriority.Medium,
                DetectedAt = DateTime.UtcNow,
                DueAt = future.EarliestDate,
                LinkText = "Visa backlog",
                LinkUrl = "/Orders/Index",
                Metrics = new[]
                {
                    new ActionCenterMetric
                    {
                        Label = "Kommande orders",
                        Value = future.OrderCount.ToString("N0", culture)
                    },
                    new ActionCenterMetric
                    {
                        Label = "Ordervärde",
                        Value = $"{future.AmountTotal:N0} kr"
                    },
                    new ActionCenterMetric
                    {
                        Label = "Sista leverans",
                        Value = latestDate
                    },
                    new ActionCenterMetric
                    {
                        Label = "Toppmånad",
                        Value = topMonth is null
                            ? "-"
                            : $"{topMonth.PeriodStart.ToString("MMM yyyy", culture)} · {topMonth.OrderCount:N0} st"
                    }
                },
                Timeline = timeline
                    .Select(bucket => new ActionCenterTimelinePoint
                    {
                        Label = bucket.PeriodStart.ToString("MMM", culture),
                        Count = bucket.OrderCount,
                        Amount = bucket.AmountTotal
                    })
                    .ToList()
            });
        }

        return insights;
    }
}

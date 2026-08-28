// Maps raw order aggregates into one consistent 12-month dashboard model for revenue KPIs and charts.
using System.Globalization;
using WebApp.Models.Dashboard;

namespace WebApp.Services.Orders;

public sealed class OrdersAnalyticsModelBuilder : IOrdersAnalyticsModelBuilder
{
    private const int RollingRevenueDays = 365;
    private const int AverageOrderValueDays = 30;
    private const int WeekBuckets = 10;
    private const int MonthBuckets = 12;
    private const int QuarterBuckets = 4;

    public RevenueDataModel BuildRevenueModel(
        IReadOnlyList<OrderTotalPoint> orders,
        IReadOnlyList<TopSellerItem> topSellers,
        bool usesFallbackPeriod)
    {
        if (!orders.Any())
        {
            return new RevenueDataModel
            {
                Kpi = new OrdersKpiModel
                {
                    AnnualRunRateMsek = 0,
                    ForecastMsek = 0,
                    AverageOrderValue = 0,
                    OrdersCountPeriod = 0
                },
                Week = new RevenueSeries { Labels = new() { "-" }, Values = new() { 0 }, XTitle = "Vecka" },
                Month = new RevenueSeries { Labels = new() { "-" }, Values = new() { 0 }, XTitle = "Månad" },
                Quarter = new RevenueSeries { Labels = new() { "-" }, Values = new() { 0 }, XTitle = "Kvartal" },
                AovLabels = new() { "-" },
                AovValues = new() { 0 },
                TopSellers = new(),
                AverageOrderValueDetails = new AverageOrderValueDetails(),
                AnnualRunRateDetails = new AnnualRunRateDetails(),
                Analysis = new RevenueAnalysisContext(),
                TotalRevenueMsek = 0
            };
        }

        // Use the freshest available order as the reference point so fallback years still get coherent KPIs.
        var referenceDate = orders.Max(o => o.OrderDate).Date;

        var rollingRevenueOrders = orders
            .Where(o => o.OrderDate.Date >= referenceDate.AddDays(-RollingRevenueDays))
            .ToList();

        var annualRunRate = rollingRevenueOrders.Any()
            ? rollingRevenueOrders.Sum(o => o.AmountInclVat) / 1_000_000m
            : orders.Sum(o => o.AmountInclVat) / 1_000_000m;

        var averageOrderValueWindow = orders
            .Where(o => o.OrderDate.Date >= referenceDate.AddDays(-AverageOrderValueDays))
            .ToList();

        var aovSource = averageOrderValueWindow.Any() ? averageOrderValueWindow : orders;
        var aovGroups = aovSource
            .GroupBy(o => o.OrderDate.Date)
            .OrderBy(g => g.Key)
            .ToList();

        var culture = CultureInfo.GetCultureInfo("sv-SE");
        var aovLabels = aovGroups
            .Select(g => g.Key.ToString("dd MMM", culture))
            .ToList();

        var aovValues = aovGroups
            .Select(g => g.Sum(x => x.AmountInclVat) / Math.Max(g.Count(), 1))
            .Select(v => Math.Round(v, 0))
            .ToList();

        var avgOrderValue = averageOrderValueWindow.Any()
            ? averageOrderValueWindow.Average(o => o.AmountInclVat)
            : orders.Average(o => o.AmountInclVat);

        var ordersCountPeriod = averageOrderValueWindow.Any() ? averageOrderValueWindow.Count : orders.Count;
        var totalRevenueMsek = Math.Round(orders.Sum(o => o.AmountInclVat) / 1_000_000m, 1);

        return new RevenueDataModel
        {
            Week = AggregateByWeek(orders, WeekBuckets),
            Month = AggregateByMonth(orders, MonthBuckets),
            Quarter = AggregateByQuarter(orders, QuarterBuckets),
            Kpi = new OrdersKpiModel
            {
                AnnualRunRateMsek = Math.Round(annualRunRate, 1),
                ForecastMsek = Math.Round(annualRunRate * 1.05m, 1),
                AverageOrderValue = Math.Round(avgOrderValue, 0),
                OrdersCountPeriod = ordersCountPeriod
            },
            AovLabels = aovLabels.Any() ? aovLabels : new() { "-" },
            AovValues = aovValues.Any() ? aovValues : new() { 0 },
            TopSellers = topSellers.ToList(),
            AverageOrderValueDetails = BuildAverageOrderValueDetails(aovSource),
            AnnualRunRateDetails = BuildAnnualRunRateDetails(rollingRevenueOrders.Any() ? rollingRevenueOrders : orders, annualRunRate),
            Analysis = BuildAnalysisContext(orders, usesFallbackPeriod),
            TotalRevenueMsek = totalRevenueMsek
        };
    }

    private static RevenueAnalysisContext BuildAnalysisContext(IReadOnlyList<OrderTotalPoint> orders, bool usesFallbackPeriod)
    {
        if (!orders.Any())
        {
            return new RevenueAnalysisContext();
        }

        return new RevenueAnalysisContext
        {
            PeriodStart = orders.Min(x => x.OrderDate).Date,
            PeriodEnd = orders.Max(x => x.OrderDate).Date,
            UsesFallbackPeriod = usesFallbackPeriod
        };
    }

    private static AverageOrderValueDetails BuildAverageOrderValueDetails(IReadOnlyList<OrderTotalPoint> orders)
    {
        if (!orders.Any())
        {
            return new AverageOrderValueDetails();
        }

        return new AverageOrderValueDetails
        {
            PeriodStart = orders.Min(x => x.OrderDate).Date,
            PeriodEnd = orders.Max(x => x.OrderDate).Date,
            OrdersCount = orders.Count,
            Orders = orders
                .OrderByDescending(x => x.OrderDate)
                .ThenByDescending(x => x.AmountInclVat)
                .Select(MapOrderDetail)
                .ToList()
        };
    }

    private static AnnualRunRateDetails BuildAnnualRunRateDetails(IReadOnlyList<OrderTotalPoint> orders, decimal annualRunRateMsek)
    {
        if (!orders.Any())
        {
            return new AnnualRunRateDetails();
        }

        return new AnnualRunRateDetails
        {
            PeriodStart = orders.Min(x => x.OrderDate).Date,
            PeriodEnd = orders.Max(x => x.OrderDate).Date,
            OrdersCount = orders.Count,
            RevenueMsek = Math.Round(annualRunRateMsek, 1),
            TopOrders = orders
                .OrderByDescending(x => x.AmountInclVat)
                .ThenByDescending(x => x.OrderDate)
                .Select(MapOrderDetail)
                .ToList()
        };
    }

    private static RevenueOrderDetail MapOrderDetail(OrderTotalPoint order)
    {
        return new RevenueOrderDetail
        {
            OrderNumber = order.OrderNumber,
            OrderLabel = !string.IsNullOrWhiteSpace(order.OrderNumberText)
                ? order.OrderNumberText!
                : $"#{order.OrderNumber}",
            OrderDate = order.OrderDate,
            AmountInclVat = Math.Round(order.AmountInclVat, 2)
        };
    }

    private static RevenueSeries AggregateByWeek(IEnumerable<OrderTotalPoint> orders, int take)
    {
        var culture = CultureInfo.GetCultureInfo("sv-SE");
        var groupedValues = orders
            .GroupBy(o => new { o.OrderDate.Year, Week = ISOWeek.GetWeekOfYear(o.OrderDate) })
            .ToDictionary(
                g => (g.Key.Year, g.Key.Week),
                g => Math.Round(g.Sum(x => x.AmountInclVat) / 1_000_000m, 2));

        var latestOrderDate = orders.Max(o => o.OrderDate);
        var latestWeekMonday = ISOWeek.ToDateTime(
            ISOWeek.GetYear(latestOrderDate),
            ISOWeek.GetWeekOfYear(latestOrderDate),
            DayOfWeek.Monday);

        var buckets = Enumerable.Range(0, take)
            .Select(offset => latestWeekMonday.AddDays(-(take - 1 - offset) * 7))
            .Select(date => new
            {
                Year = ISOWeek.GetYear(date),
                Week = ISOWeek.GetWeekOfYear(date)
            })
            .ToList();

        return new RevenueSeries
        {
            XTitle = "Vecka",
            Labels = buckets
                .Select(bucket =>
                {
                    var weekStart = ISOWeek.ToDateTime(bucket.Year, bucket.Week, DayOfWeek.Monday);
                    return $"V{bucket.Week} • {weekStart.ToString("d MMM", culture)}";
                })
                .ToList(),
            Values = buckets
                .Select(bucket => groupedValues.TryGetValue((bucket.Year, bucket.Week), out var value) ? value : 0m)
                .ToList()
        };
    }

    private static RevenueSeries AggregateByMonth(IEnumerable<OrderTotalPoint> orders, int take)
    {
        var culture = CultureInfo.GetCultureInfo("sv-SE");

        var groupedValues = orders
            .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
            .ToDictionary(
                g => (g.Key.Year, g.Key.Month),
                g => Math.Round(g.Sum(x => x.AmountInclVat) / 1_000_000m, 2));

        var latestOrderDate = orders.Max(o => o.OrderDate);
        var latestMonth = new DateTime(latestOrderDate.Year, latestOrderDate.Month, 1);
        var buckets = Enumerable.Range(0, take)
            .Select(offset => latestMonth.AddMonths(-(take - 1 - offset)))
            .ToList();

        return new RevenueSeries
        {
            XTitle = "Månad",
            Labels = buckets
                .Select(bucket => bucket.ToString("MMM yyyy", culture))
                .Select(label => label.Replace(".", ". ").Trim())
                .ToList(),
            Values = buckets
                .Select(bucket => groupedValues.TryGetValue((bucket.Year, bucket.Month), out var value) ? value : 0m)
                .ToList()
        };
    }

    private static RevenueSeries AggregateByQuarter(IEnumerable<OrderTotalPoint> orders, int take)
    {
        static int Quarter(DateTime dt) => ((dt.Month - 1) / 3) + 1;

        var groupedValues = orders
            .GroupBy(o => new { o.OrderDate.Year, Q = Quarter(o.OrderDate) })
            .ToDictionary(
                g => (g.Key.Year, g.Key.Q),
                g => Math.Round(g.Sum(x => x.AmountInclVat) / 1_000_000m, 2));

        var latestOrderDate = orders.Max(o => o.OrderDate);
        var latestQuarterStartMonth = ((Quarter(latestOrderDate) - 1) * 3) + 1;
        var latestQuarterStart = new DateTime(latestOrderDate.Year, latestQuarterStartMonth, 1);
        var buckets = Enumerable.Range(0, take)
            .Select(offset => latestQuarterStart.AddMonths(-(take - 1 - offset) * 3))
            .Select(date => new
            {
                date.Year,
                Quarter = Quarter(date)
            })
            .ToList();

        return new RevenueSeries
        {
            XTitle = "Kvartal",
            Labels = buckets.Select(bucket => $"Q{bucket.Quarter} {bucket.Year}").ToList(),
            Values = buckets
                .Select(bucket => groupedValues.TryGetValue((bucket.Year, bucket.Quarter), out var value) ? value : 0m)
                .ToList()
        };
    }
}

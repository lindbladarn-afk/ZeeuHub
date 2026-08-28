// Fetches dashboard revenue analytics with one shared 12-month default window and a visible fallback state.
using WebApp.Models.Dashboard;
using Microsoft.Extensions.Caching.Memory;

namespace WebApp.Services.Orders
{
    public sealed class OrdersAnalyticsService : IOrdersAnalyticsService
    {
        private const int RollingAnalysisDays = 365;
        private readonly IOrdersAnalyticsQueryService _queryService;
        private readonly IOrdersAnalyticsModelBuilder _modelBuilder;
        private readonly IMemoryCache _cache;

        public OrdersAnalyticsService(
            IOrdersAnalyticsQueryService queryService,
            IOrdersAnalyticsModelBuilder modelBuilder,
            IMemoryCache cache)
        {
            _queryService = queryService;
            _modelBuilder = modelBuilder;
            _cache = cache;
        }

        public async Task<RevenueDataModel> GetRevenueAsync(string connectionString, int? companyCode)
        {
            var cacheKey = BuildRevenueCacheKey(connectionString, companyCode);

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(3);

                var today = DateTime.UtcNow.Date;
                var analysisFromDate = today.AddDays(-RollingAnalysisDays);
                var usesFallbackPeriod = false;

                // Keep revenue and top sellers on the same default window so the dashboard tells one story.
                var orders = await _queryService.GetOrderTotalsAsync(connectionString, companyCode, analysisFromDate);
                var topSellers = await _queryService.GetTopSellersAsync(connectionString, companyCode, fromDate: analysisFromDate);

                if (orders.Count == 0)
                {
                    var latestOrderDate = await _queryService.GetLatestOrderDateAsync(connectionString, companyCode);
                    if (latestOrderDate.HasValue)
                    {
                        usesFallbackPeriod = true;
                        var fallbackFromDate = new DateTime(latestOrderDate.Value.Year, 1, 1);
                        orders = await _queryService.GetOrderTotalsAsync(connectionString, companyCode, fallbackFromDate);
                        topSellers = await _queryService.GetTopSellersAsync(connectionString, companyCode, fromDate: fallbackFromDate);
                    }
                }

                return _modelBuilder.BuildRevenueModel(orders, topSellers, usesFallbackPeriod);
            }) ?? new RevenueDataModel();
        }

        private static string BuildRevenueCacheKey(string connectionString, int? companyCode)
        {
            return string.Join("|",
                "dashboard-revenue",
                companyCode?.ToString() ?? "null",
                connectionString?.GetHashCode().ToString() ?? "null");
        }
    }
}

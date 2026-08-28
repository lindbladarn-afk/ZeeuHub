using WebApp.Models.Dashboard;

namespace WebApp.Services.Orders;

// Reads the raw order analytics data needed to build dashboard revenue models.
public interface IOrdersAnalyticsQueryService
{
    Task<IReadOnlyList<OrderTotalPoint>> GetOrderTotalsAsync(string connectionString, int? companyCode, DateTime? fromDate = null);
    Task<IReadOnlyList<TopSellerItem>> GetTopSellersAsync(string connectionString, int? companyCode, int take = 6, DateTime? fromDate = null);
    Task<DateTime?> GetLatestOrderDateAsync(string connectionString, int? companyCode);
}

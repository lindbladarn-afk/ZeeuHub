// Builds one coherent dashboard analytics model from already-fetched order and seller data.
using WebApp.Models.Dashboard;

namespace WebApp.Services.Orders;

public interface IOrdersAnalyticsModelBuilder
{
    RevenueDataModel BuildRevenueModel(
        IReadOnlyList<OrderTotalPoint> orders,
        IReadOnlyList<TopSellerItem> topSellers,
        bool usesFallbackPeriod);
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebApp.Models.Orders;

namespace WebApp.Repositories.Orders
{
    public interface IOrderDataRepository
    {
        Task<PagedOrdersPageResultDto> GetOrdersPageAsync(string connectionString, GetOrdersQuery query);
        Task<OrdersSummaryDto> GetOrdersSummaryAsync(string connectionString, GetOrdersQuery query);
        Task<DateTime?> GetLatestOrderDateAsync(string connectionString, int? companyCode);
        Task<OrderDeliveryInsightSummaryDto> GetOverdueDeliverySummaryAsync(string connectionString, GetOrderDeliveryInsightQuery query);
        Task<OrderDeliveryInsightSummaryDto> GetFutureDeliverySummaryAsync(string connectionString, GetDeliveryForecastQuery query);
        Task<IReadOnlyList<OrderDeliveryTimelineBucketDto>> GetFutureDeliveryTimelineAsync(string connectionString, GetDeliveryForecastQuery query);
        Task<OrderWithLinesDto?> GetOrderWithLinesAsync(string connectionString, GetOrderDetailsQuery query);
        Task<IReadOnlyList<OrderCustomerOption>> GetFutureDeliveryCustomerOptionsAsync(string connectionString, GetDeliveryForecastQuery query);
        Task<PagedOrdersPageResultDto> GetUpcomingOrdersPageAsync(string connectionString, GetDeliveryForecastQuery query);
    }
}

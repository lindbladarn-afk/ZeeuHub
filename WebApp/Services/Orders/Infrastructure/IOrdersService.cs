using System.Collections.Generic;
using System.Threading.Tasks;
using WebApp.Models.Orders;

namespace WebApp.Services.Orders
{
    public interface IOrdersService
    {
        Task<OrdersListViewModel> GetOrdersAsync(string connectionString, GetOrdersQuery query);
        Task<OrderDetailsViewModel?> GetOrderDetailsAsync(string connectionString, GetOrderDetailsQuery query);
        Task<OrderDeliveryForecastViewModel> GetDeliveryForecastAsync(string connectionString, GetDeliveryForecastQuery query);
    }
}

using System.Threading.Tasks;
using WebApp.Models.Dashboard;

namespace WebApp.Services.Orders
{
    public interface IOrdersAnalyticsService
    {
        Task<RevenueDataModel> GetRevenueAsync(string connectionString, int? companyCode);
    }
}

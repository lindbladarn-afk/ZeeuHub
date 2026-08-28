using System.Threading.Tasks;
using WebApp.Models.CustomerActivity;

namespace WebApp.Services.CustomerActivity
{
    public interface ICustomerActivityService
    {
        Task<CustomerActivityViewModel> GetRecentAsync(string? connectionString, int? companyCode, int take = 5);
    }
}

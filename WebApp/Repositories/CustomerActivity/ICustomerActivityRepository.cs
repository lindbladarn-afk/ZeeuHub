using System.Collections.Generic;
using System.Threading.Tasks;
using WebApp.Models.CustomerActivity;

namespace WebApp.Repositories.CustomerActivity
{
    public interface ICustomerActivityRepository
    {
        Task<IReadOnlyList<CustomerActivityDto>> GetRecentAsync(string connectionString, int companyCode, int take);
    }
}

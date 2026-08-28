using Entities.Customers;

namespace WebApp.Repositories.Customers;

public interface ICustomerRepository
{
    Task<IEnumerable<ICustomerViewModel>> GetAllCustomersAsync(string connectionString, int? customerCode = null);
    Task<IEnumerable<ICustomersAutoCompleteDto>> GetAutoCompleteCustomersAsync(string connectionString, int? customerCode = null);
}

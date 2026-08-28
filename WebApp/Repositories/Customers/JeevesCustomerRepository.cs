using System.Data;
using Dapper;
using Entities.Customers;
using Repository.Execution;

namespace WebApp.Repositories.Customers;

public class JeevesCustomerRepository : ICustomerRepository
{
    private readonly IJeevesSqlExecutor _jeevesSqlExecutor;

    public JeevesCustomerRepository(IJeevesSqlExecutor jeevesSqlExecutor)
    {
        _jeevesSqlExecutor = jeevesSqlExecutor;
    }

    public async Task<IEnumerable<ICustomerViewModel>> GetAllCustomersAsync(string connectionString, int? customerCode = null)
    {
        const string query = "q_zu_CustomerPortal_Customer";

        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetAllCustomers");
        param.Add("ForetagKod", customerCode);

        return await _jeevesSqlExecutor.QueryAsync<CustomerViewModel>(
            connectionString,
            query,
            param,
            CommandType.StoredProcedure,
            operationName: "JeevesCustomerRepository.GetAllCustomers");
    }

    public async Task<IEnumerable<ICustomersAutoCompleteDto>> GetAutoCompleteCustomersAsync(string connectionString, int? customerCode = null)
    {
        const string query = "q_zu_CustomerPortal_Customer";

        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetAutoCompleteCustomers");
        param.Add("ForetagKod", customerCode);

        return await _jeevesSqlExecutor.QueryAsync<CustomersAutoCompleteDto>(
            connectionString,
            query,
            param,
            CommandType.StoredProcedure,
            operationName: "JeevesCustomerRepository.GetAutoCompleteCustomers");
    }
}

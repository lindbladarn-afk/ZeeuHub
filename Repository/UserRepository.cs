namespace Repository;

public class UserRepository : IUserRepository
{
    private readonly Repository.Execution.IJeevesSqlExecutor _jeevesSqlExecutor;

    public UserRepository(Repository.Execution.IJeevesSqlExecutor jeevesSqlExecutor)
    {
        _jeevesSqlExecutor = jeevesSqlExecutor;
    }

    public async Task<IEnumerable<JeevesCompanyVM>> GetJeevesCompaniesAsync(string connectionString, string persSign)
    {
        var query = "q_zu_CustomerPortal_User";

        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetJeevesCompanies");
        param.Add("PersSign", persSign);

        return await _jeevesSqlExecutor.QueryAsync<JeevesCompanyVM>(
            connectionString,
            query,
            param,
            CommandType.StoredProcedure,
            operationName: "UserRepository.GetJeevesCompanies");
    }
}

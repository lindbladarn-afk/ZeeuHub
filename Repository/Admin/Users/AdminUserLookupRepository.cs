namespace Repository;

public class AdminUserLookupRepository : IAdminUserLookupRepository
{
    private readonly string _sqlConnectionString;

    public AdminUserLookupRepository()
    {
        _sqlConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING_PORTAL_IDENTITY")
            ?? throw new ArgumentNullException("Environment variable for portal identity could not be found");
    }

    public async Task<ManageCompanyVM?> GetUserCompany(string userId)
    {
        using var connection = new SqlConnection(_sqlConnectionString);
        var query = "spr_Admin";
        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetUserCompany");
        param.Add("UserId", userId);

        try
        {
            return (await connection.QueryAsync<ManageCompanyVM>(query, param, commandType: CommandType.StoredProcedure)).FirstOrDefault();
        }
        catch (SqlException sqlEx)
        {
            await Console.Out.WriteLineAsync($"Something went wrong in SQL: {sqlEx.Message}");
        }
        catch (Exception ex)
        {
            await Console.Out.WriteLineAsync($"Something went wrong: {ex.Message}");
        }

        return null;
    }

    public async Task<IEnumerable<UserCompanyLookup>> GetUserCompaniesLookup()
    {
        using var connection = new SqlConnection(_sqlConnectionString);
        var query = "spr_Admin";
        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetUserCompaniesLookup");

        return await connection.QueryAsync<UserCompanyLookup>(query, param, commandType: CommandType.StoredProcedure);
    }
}

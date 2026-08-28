namespace Repository;

public class ApplicationUserRepository : IApplicationUserRepository
{
    private readonly string _identitySqlConnectionString;

    public ApplicationUserRepository()
    {
        _identitySqlConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING_PORTAL_IDENTITY")
            ?? throw new ArgumentNullException(nameof(_identitySqlConnectionString));
    }

    public async Task<IUser> GetUserAsync(string userId)
    {
        using var connection = new SqlConnection(_identitySqlConnectionString);

        const string query = "spr_Application";
        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetUser");
        param.Add("UserId", userId);

        using var multi = await connection.QueryMultipleAsync(query, param, commandType: CommandType.StoredProcedure);
        var user = multi.Read<User>().FirstOrDefault();
        var company = multi.Read<Company>().FirstOrDefault();

        user.Company = company;

        return user;
    }
}

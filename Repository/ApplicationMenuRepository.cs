namespace Repository;

public class ApplicationMenuRepository : IApplicationMenuRepository
{
    private readonly string _identitySqlConnectionString;

    public ApplicationMenuRepository()
    {
        _identitySqlConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING_PORTAL_IDENTITY")
            ?? throw new ArgumentNullException(nameof(_identitySqlConnectionString));
    }

    public async Task<SideMenuViewModel> GetMenuAsync(Guid companyId)
    {
        using var connection = new SqlConnection(_identitySqlConnectionString);

        const string query = "spr_ModulesAndSubModules";
        var param = new DynamicParameters();
        param.Add("SelectStatement", "ModulesAndPermissions");
        param.Add("CompanyId", companyId);

        using var multi = connection.QueryMultiple(query, param, commandType: CommandType.StoredProcedure);
        var modules = (await multi.ReadAsync<SideMenuModulesViewModel>()).ToList();
        var subModules = (await multi.ReadAsync<SideMenuSubModuleViewModel>()).ToList();
        var permittedSubModules = (await connection.QueryAsync<Guid?>(
                "SELECT SubModuleId FROM [Identity].[CompanyPermissions] WHERE CompanyId = @CompanyId AND SubModuleId IS NOT NULL",
                new { CompanyId = companyId }))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        foreach (var subModule in subModules)
        {
            subModule.UserHasPermission = permittedSubModules.Contains(subModule.Id);
        }

        foreach (var module in modules)
        {
            module.SubModules = subModules
                .Where(z => z.ModuleId == module.Id && z.MenuItemEnabled == true)
                .ToList();
        }

        return new SideMenuViewModel { Modules = modules };
    }
}

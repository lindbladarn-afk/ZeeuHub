namespace Repository;

public class AdminCompanyRepository : IAdminCompanyRepository
{
    private readonly string _sqlConnectionString;

    public AdminCompanyRepository()
    {
        _sqlConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING_PORTAL_IDENTITY")
            ?? throw new ArgumentNullException("Environment variable for portal identity could not be found");
    }

    public async Task<int> GetCompanyCountAsync()
    {
        const string query = "SELECT COUNT(1) FROM [Identity].[Companies];";
        using var connection = new SqlConnection(_sqlConnectionString);
        return await connection.ExecuteScalarAsync<int>(query);
    }

    public async Task<List<ManageCompanyVM>> GetCompanies()
    {
        var query = "spr_Admin";

        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetAllCompanies");

        using var connection = new SqlConnection(_sqlConnectionString);
        using var multi = connection.QueryMultiple(query, param, commandType: CommandType.StoredProcedure);

        var companies = (await multi.ReadAsync<ManageCompanyVM>()).ToList();
        var companyPermissions = (await multi.ReadAsync<AdminCompanyPermissionsViewModel>()).ToList();
        var modules = (await multi.ReadAsync<AdminModuleViewModel>()).ToList();
        var subModules = (await multi.ReadAsync<AdminSubModuleViewModel>()).ToList();
        var connectionStrings = (await multi.ReadAsync<AdminCompanyConnectionStringViewModel>()).ToList();
        var connectionStringTypes = (await multi.ReadAsync<AdminCompanyConnectionStringTypeViewModel>()).ToList();
        var defaultCompanyCodes = (await connection.QueryAsync<CompanyCodeRow>(
            "SELECT Id, DefaultJeevesCompanyCode FROM [Identity].[Companies];")).ToDictionary(x => x.Id, x => x.DefaultJeevesCompanyCode);

        PopulatePermissionsWithNames(companyPermissions, modules, subModules);
        PopulateConnectionStringsWithType(connectionStrings, connectionStringTypes);

        foreach (var company in companies)
        {
            if (defaultCompanyCodes.TryGetValue(company.Id, out var companyCode))
                company.DefaultJeevesCompanyCode = companyCode;
            company.Permissions = companyPermissions.Where(x => x.CompanyId == company.Id).ToList();
            company.ConnectionStrings = connectionStrings.Where(x => x.CompanyId == company.Id).ToList();
        }

        return companies;
    }

    public async Task<ManageCompanyVM?> GetCompanyByIdAsync(Guid companyId)
    {
        const string query = "spr_Admin";
        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetCompanyById");
        param.Add("CompanyId", companyId);

        using var connection = new SqlConnection(_sqlConnectionString);
        using var multi = await connection.QueryMultipleAsync(query, param, commandType: CommandType.StoredProcedure);

        var company = await multi.ReadFirstOrDefaultAsync<ManageCompanyVM>();
        if (company == null) return null;

        var companyPermissions = (await multi.ReadAsync<AdminCompanyPermissionsViewModel>()).ToList();
        var modules = (await multi.ReadAsync<AdminModuleViewModel>()).ToList();
        var subModules = (await multi.ReadAsync<AdminSubModuleViewModel>()).ToList();
        var connectionStrings = (await multi.ReadAsync<AdminCompanyConnectionStringViewModel>()).ToList();
        var connectionStringTypes = (await multi.ReadAsync<AdminCompanyConnectionStringTypeViewModel>()).ToList();
        company.DefaultJeevesCompanyCode = await connection.ExecuteScalarAsync<int?>(
            "SELECT DefaultJeevesCompanyCode FROM [Identity].[Companies] WHERE Id = @CompanyId;",
            new { CompanyId = companyId });

        PopulatePermissionsWithNames(companyPermissions, modules, subModules);
        PopulateModulesWithSubModules(modules, subModules);
        PopulateConnectionStringsWithType(connectionStrings, connectionStringTypes);
        PopulateConnectionStringsWithRedactedConnectionString(connectionStrings);

        company.AllModules = modules;
        company.Permissions = companyPermissions.Where(x => x.CompanyId == company.Id).ToList();
        company.ConnectionStrings = connectionStrings.Where(x => x.CompanyId == company.Id).ToList();

        return company;
    }

    public async Task<IEnumerable<AdminAllCompaniesForSelectListVM>> GetAllCompaniesForSelectList()
    {
        var query = "spr_Admin";
        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetAllSelectListCompanies");

        using var connection = new SqlConnection(_sqlConnectionString);
        return await connection.QueryAsync<AdminAllCompaniesForSelectListVM>(query, param, commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<AdminCompanyConnectionStringViewModel>> GetCompanyConnectionStringsForSelectListAsync(Guid? companyId = null)
    {
        using var connection = new SqlConnection(_sqlConnectionString);
        var query = "spr_Admin";
        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetAllSelectListConnectionStrings");
        param.Add("CompanyId", companyId);

        return await connection.QueryAsync<AdminCompanyConnectionStringViewModel>(query, param, commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<AdminCompanyConnectionStringTypeViewModel>> GetConnectionStringTypesAsync()
    {
        using var connection = new SqlConnection(_sqlConnectionString);
        const string query = "spr_Admin";
        var param = new DynamicParameters();
        param.Add("SelectStatement", "GetConnectionStringTypes");

        return await connection.QueryAsync<AdminCompanyConnectionStringTypeViewModel>(query, param, commandType: CommandType.StoredProcedure);
    }

    public async Task<Guid> CreateCompanyAsync(AdminCreateCompanyViewModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        var companyId = model.CompanyId != Guid.Empty ? model.CompanyId : Guid.NewGuid();
        var connectionStringId = model.ConnectionStringId != Guid.Empty ? model.ConnectionStringId : Guid.NewGuid();

        using var connection = new SqlConnection(_sqlConnectionString);
        const string query = "spr_Company";
        var param = new DynamicParameters();
        param.Add("SelectStatement", "CreateCompany");
        param.Add("CompanyId", companyId);
        param.Add("CompanyName", model.Name);
        param.Add("ConnectionStringId", connectionStringId);
        param.Add("ConnectionStringTypeId", model.ConnectionStringTypeId);
        param.Add("DatabaseName", model.DatabaseName);
        param.Add("IsActive", true);

        try
        {
            await connection.ExecuteAsync(query, param, commandType: CommandType.StoredProcedure);
        }
        catch (SqlException ex) when (ex.Number == 8144)
        {
            using var tx = connection.BeginTransaction();

            const string insertCompany = @"
IF NOT EXISTS (SELECT 1 FROM [Identity].[Companies] WHERE Id = @CompanyId)
BEGIN
    INSERT INTO [Identity].[Companies] (Id, Name, DefaultJeevesCompanyCode)
    VALUES (@CompanyId, @CompanyName, @DefaultJeevesCompanyCode);
END";

            await connection.ExecuteAsync(insertCompany, new
            {
                CompanyId = companyId,
                CompanyName = model.Name,
                model.DefaultJeevesCompanyCode
            }, tx);

            const string insertConnectionString = @"
IF NOT EXISTS (SELECT 1 FROM [Identity].[ConnectionStrings] WHERE Id = @ConnectionStringId)
BEGIN
    INSERT INTO [Identity].[ConnectionStrings] (Id, CompanyId, ConnectionStringTypeId, DatabaseName, IsActive)
    VALUES (@ConnectionStringId, @CompanyId, @ConnectionStringTypeId, @DatabaseName, @IsActive);
END";

            await connection.ExecuteAsync(insertConnectionString, new
            {
                ConnectionStringId = connectionStringId,
                CompanyId = companyId,
                ConnectionStringTypeId = model.ConnectionStringTypeId,
                DatabaseName = model.DatabaseName,
                IsActive = true
            }, tx);

            tx.Commit();
        }

        await connection.ExecuteAsync(
            "UPDATE [Identity].[Companies] SET DefaultJeevesCompanyCode = @DefaultJeevesCompanyCode WHERE Id = @CompanyId;",
            new { model.DefaultJeevesCompanyCode, CompanyId = companyId });

        var envKey = $"CONNECTION_STRING_{connectionStringId.ToString().ToUpper().Replace("-", string.Empty)}";
        model.EnvironmentVariableName = envKey;
        model.CompanyId = companyId;
        model.ConnectionStringId = connectionStringId;

        return companyId;
    }

    public async Task UpdateCompanyAsync(Company company)
    {
        using var connection = new SqlConnection(_sqlConnectionString);
        var query = "spr_Company";
        var param = new DynamicParameters();
        param.Add("SelectStatement", "UpdateCompany");
        param.Add("CompanyId", company.Id);
        param.Add("CompanyName", company.Name);
        await connection.ExecuteAsync(query, param, commandType: CommandType.StoredProcedure);
        await connection.ExecuteAsync(
            "UPDATE [Identity].[Companies] SET DefaultJeevesCompanyCode = @DefaultJeevesCompanyCode WHERE Id = @CompanyId;",
            new { company.DefaultJeevesCompanyCode, CompanyId = company.Id });
    }

    public async Task AddCompanyPermission(CompanyPermission companyPermission)
    {
        using var connection = new SqlConnection(_sqlConnectionString);
        var query = "spr_Company";
        var param = new DynamicParameters();
        param.Add("SelectStatement", "AddCompanyPermission");
        param.Add("CompanyPermissionId", companyPermission.Id);
        param.Add("CompanyId", companyPermission.CompanyId);
        param.Add("ModuleId", companyPermission.ModuleId);
        param.Add("SubModuleId", companyPermission.SubModuleId);
        await connection.ExecuteAsync(query, param, commandType: CommandType.StoredProcedure);
    }

    public async Task RemoveCompanyPermission(Guid permissionId)
    {
        using var connection = new SqlConnection(_sqlConnectionString);
        var query = "spr_Company";
        var param = new DynamicParameters();
        param.Add("SelectStatement", "RemoveCompanyPermission");
        param.Add("CompanyPermissionId", permissionId);
        await connection.ExecuteAsync(query, param, commandType: CommandType.StoredProcedure);
    }

    public async Task RemoveCompanyPermission(Guid companyId, Guid subModuleId)
    {
        const string query = @"DELETE FROM [Identity].[CompanyPermissions]
WHERE CompanyId = @CompanyId AND SubModuleId = @SubModuleId;";

        using var connection = new SqlConnection(_sqlConnectionString);
        await connection.ExecuteAsync(query, new { CompanyId = companyId, SubModuleId = subModuleId });
    }

    private static void PopulatePermissionsWithNames(
        List<AdminCompanyPermissionsViewModel> companyPermissions,
        List<AdminModuleViewModel> modules,
        List<AdminSubModuleViewModel> subModules)
    {
        for (int i = 0; i < companyPermissions.Count; i++)
        {
            companyPermissions[i].ModuleName = modules.FirstOrDefault(x => x.Id == companyPermissions[i].ModuleId)?.Name;
            companyPermissions[i].SubModuleName = subModules.FirstOrDefault(x => x.Id == companyPermissions[i].SubModuleId)?.Name;
            var subModule = subModules.FirstOrDefault(x => x.Id == companyPermissions[i].SubModuleId);
            if (subModule is null) continue;
            subModule.HasAccess = true;
            subModule.PermissionId = companyPermissions[i].Id;
        }
    }

    private static void PopulateModulesWithSubModules(List<AdminModuleViewModel> modules, List<AdminSubModuleViewModel> subModules)
    {
        for (int i = 0; i < modules.Count; i++)
        {
            modules[i].SubModules = subModules.Where(x => x.ModuleId == modules[i].Id).ToList();
        }
    }

    private static void PopulateConnectionStringsWithType(
        List<AdminCompanyConnectionStringViewModel> connectionStrings,
        List<AdminCompanyConnectionStringTypeViewModel> connectionStringTypes)
    {
        for (int i = 0; i < connectionStrings.Count; i++)
        {
            connectionStrings[i].ConnectionStringType =
                connectionStringTypes.FirstOrDefault(x => x.Id == connectionStrings[i].ConnectionStringTypeId);
        }
    }

    private static void PopulateConnectionStringsWithRedactedConnectionString(List<AdminCompanyConnectionStringViewModel> connectionStrings)
    {
        for (int i = 0; i < connectionStrings.Count; i++)
        {
            var connectionString = Environment.GetEnvironmentVariable($"CONNECTION_STRING_{connectionStrings[i].Id.ToString().ToUpper().Replace("-", "")}");
            connectionStrings[i].ConnectionString = PartiallyHideConnectionString(connectionString);
        }
    }

    private static string PartiallyHideConnectionString(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

        if (builder.ContainsKey("User Id"))
            builder["User Id"] = HideValue(builder["User Id"].ToString());

        if (builder.ContainsKey("User"))
            builder["User"] = HideValue(builder["User"].ToString());

        if (builder.ContainsKey("Password"))
            builder["Password"] = HideValue(builder["Password"].ToString());

        return builder.ToString();
    }

    private static string HideValue(string value, bool fullyHidden = true)
    {
        if (fullyHidden)
        {
            return new string('*', 8);
        }

        var visibleChars = Math.Min(3, value.Length);
        return value.Substring(0, visibleChars) + new string('*', value.Length - visibleChars);
    }

    private sealed class CompanyCodeRow
    {
        public Guid Id { get; set; }
        public int? DefaultJeevesCompanyCode { get; set; }
    }
}

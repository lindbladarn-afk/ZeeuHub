using Entities.Application;
using Entities.ViewModels.Admin;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Repository.Execution;
using WebApp.Data;
using WebApp.Helpers;
using WebApp.Mapping;
using WebApp.Models.Identity;
using WebApp.Models.AI;
using WebApp.Services.Application;

namespace WebApp.Services.Admin;

// This service owns company administration use cases in the admin area.
// It keeps company CRUD, permission updates, and connection checks out of the controller.
public class AdminCompanyManagementService : IAdminCompanyManagementService
{
    private static readonly Guid LocalDevelopmentTypeId =
        Guid.Parse("0e02e3cc-0fea-4aff-9311-204b4eb6c0d4");

    private readonly Repository.Contracts.IAdminCompanyRepository _adminCompanyRepository;
    private readonly IApplicationConnectionContextService _applicationConnectionContextService;
    private readonly IConnectionStringResolver _connectionStringResolver;
    private readonly ApplicationDbContext _context;
    private readonly Repository.Contracts.IUserRepository _userRepository;
    private readonly IJeevesSqlExecutor _jeevesSqlExecutor;
    private readonly IApplicationMenuService _applicationMenuService;

    public AdminCompanyManagementService(
        Repository.Contracts.IAdminCompanyRepository adminCompanyRepository,
        IApplicationConnectionContextService applicationConnectionContextService,
        IConnectionStringResolver connectionStringResolver,
        ApplicationDbContext context,
        Repository.Contracts.IUserRepository userRepository,
        IJeevesSqlExecutor jeevesSqlExecutor,
        IApplicationMenuService applicationMenuService)
    {
        _adminCompanyRepository = adminCompanyRepository;
        _applicationConnectionContextService = applicationConnectionContextService;
        _connectionStringResolver = connectionStringResolver;
        _context = context;
        _userRepository = userRepository;
        _jeevesSqlExecutor = jeevesSqlExecutor;
        _applicationMenuService = applicationMenuService;
    }

    public async Task<IReadOnlyCollection<ManageCompanyVM>> GetCompaniesAsync()
    {
        return await _adminCompanyRepository.GetCompanies();
    }

    public AdminCreateCompanyViewModel BuildCreateCompanyViewModel()
    {
        var model = new AdminCreateCompanyViewModel
        {
            CompanyId = Guid.NewGuid(),
            ConnectionStringId = Guid.NewGuid(),
            ConnectionStringTypeId = LocalDevelopmentTypeId
        };

        model.EnvironmentVariableName = $"CONNECTION_STRING_{model.ConnectionStringId:N}".ToUpperInvariant();
        return model;
    }

    public async Task<AdminCreateCompanyResult> CreateCompanyAsync(AdminCreateCompanyViewModel model)
    {
        if (!ValidatorTryValidate(model))
        {
            if (model.CompanyId == Guid.Empty) model.CompanyId = Guid.NewGuid();
            if (model.ConnectionStringId == Guid.Empty) model.ConnectionStringId = Guid.NewGuid();
            if (model.ConnectionStringTypeId == Guid.Empty) model.ConnectionStringTypeId = LocalDevelopmentTypeId;

            model.EnvironmentVariableName = $"CONNECTION_STRING_{model.ConnectionStringId:N}".ToUpperInvariant();
            return new AdminCreateCompanyResult
            {
                Model = model,
                ShouldReturnView = true
            };
        }

        model.ConnectionStringTypeId = LocalDevelopmentTypeId;

        if (model.CompanyId == Guid.Empty)
            model.CompanyId = Guid.NewGuid();

        if (model.ConnectionStringId == Guid.Empty)
            model.ConnectionStringId = Guid.NewGuid();

        model.EnvironmentVariableName = $"CONNECTION_STRING_{model.ConnectionStringId:N}".ToUpperInvariant();

        var companyId = await _adminCompanyRepository.CreateCompanyAsync(model);
        _applicationMenuService.Invalidate(companyId);
        return new AdminCreateCompanyResult
        {
            CreatedCompanyId = companyId,
            SuccessMessage = $"Företag {model.Name} skapades. Sätt env-var '{model.EnvironmentVariableName}' i Azure/Docker till rätt connection string."
        };
    }

    public async Task<ManageCompanyVM?> GetManageCompanyAsync(Guid companyId)
    {
        var model = await _adminCompanyRepository.GetCompanyByIdAsync(companyId);
        if (model is null)
            return null;

        FilterHiddenIntegrationSubModules(model);

        var companySettings = await _context.Companies!
            .AsNoTracking()
            .Where(x => x.Id == companyId)
            .Select(x => new { x.AiDataProfile, x.AiAllowDataSourceSwitching, x.AiPrimaryConnectionStringId })
            .FirstOrDefaultAsync();
        if (companySettings is not null)
        {
            model.AiDataProfile = AiDataProfile.Normalize(companySettings.AiDataProfile);
            model.AiAllowDataSourceSwitching = companySettings.AiAllowDataSourceSwitching;
            model.AiPrimaryConnectionStringId = companySettings.AiPrimaryConnectionStringId;
        }

        var aiConnections = await _context.ConnectionStrings!
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => new { x.Id, x.AiDataProfile, x.IsAiEnabled })
            .ToListAsync();
        foreach (var connection in model.ConnectionStrings ?? [])
        {
            var aiConnection = aiConnections.FirstOrDefault(x => x.Id == connection.Id);
            if (aiConnection is null) continue;
            connection.AiDataProfile = AiDataProfile.Normalize(aiConnection.AiDataProfile);
            connection.IsAiEnabled = aiConnection.IsAiEnabled;
        }

        model.JeevesCompanies = await _context.CompanyJeevesCompanies!
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.IsDefault)
            .ThenBy(x => x.CompanyCode)
            .Select(x => new AdminCompanyJeevesCompanyViewModel
            {
                Id = x.Id,
                CompanyCode = x.CompanyCode,
                DisplayName = x.DisplayName,
                IsDefault = x.IsDefault,
                IsActive = x.IsActive,
                SortOrder = x.SortOrder
            })
            .ToListAsync();

        return model;
    }

    public async Task<AdminConnectionTestResult> TestCompanyConnectionAsync(Guid companyId, Guid connectionStringId)
    {
        if (companyId == Guid.Empty || connectionStringId == Guid.Empty)
            return new AdminConnectionTestResult { Message = "Missing companyId or connectionStringId." };

        var resolved = await ResolveCompanyConnectionAsync(companyId, connectionStringId);
        if (!resolved.Success)
            return new AdminConnectionTestResult { Message = resolved.Error ?? "Could not resolve connection string." };

        try
        {
            await using var conn = new SqlConnection(resolved.Value);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT 1;", conn);
            await cmd.ExecuteScalarAsync();
            return new AdminConnectionTestResult { Success = true, Message = "OK" };
        }
        catch (Exception ex)
        {
            return new AdminConnectionTestResult { Message = ex.Message };
        }
    }

    public async Task<AdminJeevesCompaniesResult> GetJeevesCompaniesAsync(Guid companyId, Guid connectionStringId, string persSign)
    {
        if (companyId == Guid.Empty || connectionStringId == Guid.Empty)
            return new AdminJeevesCompaniesResult { Message = "Missing companyId or connectionStringId." };

        if (string.IsNullOrWhiteSpace(persSign))
            return new AdminJeevesCompaniesResult { Message = "PersSign is required." };

        var resolved = await ResolveCompanyConnectionAsync(companyId, connectionStringId);
        if (!resolved.Success || string.IsNullOrWhiteSpace(resolved.Value))
            return new AdminJeevesCompaniesResult { Message = resolved.Error ?? "Could not resolve connection string." };

        try
        {
            var companies = await _userRepository.GetJeevesCompaniesAsync(resolved.Value, persSign.Trim());
            return new AdminJeevesCompaniesResult
            {
                Success = true,
                Items = companies?.ToList() ?? new List<Entities.User.JeevesCompanyVM>()
            };
        }
        catch (Exception ex)
        {
            return new AdminJeevesCompaniesResult { Message = ex.Message };
        }
    }

    public async Task<AdminManageCompanyResult> UpdateCompanyAsync(ManageCompanyVM model)
    {
        var company = await _adminCompanyRepository.GetCompanyByIdAsync(model.Id);
        if (company is null)
        {
            return new AdminManageCompanyResult
            {
                RedirectToCompanies = true,
                ErrorMessages = new List<string> { "Could not find company." }
            };
        }

        var result = new AdminManageCompanyResult
        {
            Model = model
        };
        var companySettingsUpdated = false;
        var permissionsUpdated = false;

        if (!string.Equals(model.Name, company.Name, StringComparison.Ordinal))
        {
            company.Name = model.Name;

            var mappedCompany = company.ToDomainCompany();
            await _adminCompanyRepository.UpdateCompanyAsync(mappedCompany);
            companySettingsUpdated = true;

            result.SuccessMessages.Add($"Company settings were updated for {company.Name}");
        }

        var normalizedAiProfile = AiDataProfile.Normalize(model.AiDataProfile);
        if (!AiDataProfile.IsSupported(model.AiDataProfile))
        {
            result.ErrorMessages.Add("Ogiltig AI-dataprofil.");
            result.Model = await RehydrateManageCompanyForErrorsAsync(model);
            return result;
        }

        var companyEntity = await _context.Companies!.SingleOrDefaultAsync(x => x.Id == model.Id);
        if (companyEntity is null)
        {
            result.ErrorMessages.Add("Kunde inte läsa bolagets AI-inställningar.");
            return result;
        }

        var postedAiConnections = model.ConnectionStrings ?? [];
        var enabledIds = postedAiConnections.Where(x => x.IsAiEnabled).Select(x => x.Id).ToHashSet();
        if (model.AiPrimaryConnectionStringId.HasValue && !enabledIds.Contains(model.AiPrimaryConnectionStringId.Value))
        {
            result.ErrorMessages.Add("Den primära AI-anslutningen måste vara markerad som tillåten för AI.");
            result.Model = await RehydrateManageCompanyForErrorsAsync(model);
            return result;
        }

        var dbConnections = await _context.ConnectionStrings!.Where(x => x.CompanyId == model.Id).ToListAsync();
        var aiConnectionsChanged = false;
        foreach (var connection in dbConnections)
        {
            var posted = postedAiConnections.FirstOrDefault(x => x.Id == connection.Id);
            if (posted is null) continue;
            var postedProfile = AiDataProfile.Normalize(posted.AiDataProfile);
            aiConnectionsChanged |= connection.IsAiEnabled != posted.IsAiEnabled ||
                                    !string.Equals(connection.AiDataProfile, postedProfile, StringComparison.Ordinal);
            connection.IsAiEnabled = posted.IsAiEnabled;
            connection.AiDataProfile = postedProfile;
        }

        if (!string.Equals(companyEntity.AiDataProfile, normalizedAiProfile, StringComparison.Ordinal) ||
            companyEntity.AiAllowDataSourceSwitching != model.AiAllowDataSourceSwitching ||
            companyEntity.AiPrimaryConnectionStringId != model.AiPrimaryConnectionStringId ||
            aiConnectionsChanged)
        {
            companyEntity.AiDataProfile = normalizedAiProfile;
            companyEntity.AiAllowDataSourceSwitching = model.AiAllowDataSourceSwitching;
            companyEntity.AiPrimaryConnectionStringId = model.AiPrimaryConnectionStringId;
            await _context.SaveChangesAsync();
            companySettingsUpdated = true;
            result.SuccessMessages.Add("Uppdaterade AI-datakällans policy för bolaget.");
        }

        var jeevesCompaniesChanged = await HasJeevesCompanyConfigurationChangedAsync(model);
        if (jeevesCompaniesChanged)
        {
            var jeevesCompanyErrors = await SyncCompanyJeevesCompaniesAsync(model);
            if (jeevesCompanyErrors.Count > 0)
            {
                result.ErrorMessages.AddRange(jeevesCompanyErrors);
                result.Model = await RehydrateManageCompanyForErrorsAsync(model);
                return result;
            }
        }

        if (model.AllModules is not null)
        {
            for (int i = 0; i < model.AllModules.Count; i++)
            {
                var module = model.AllModules[i];
                if (module.SubModules is null) continue;

                for (int j = 0; j < module.SubModules.Count; j++)
                {
                    var sub = module.SubModules[j];
                    var permissionId = sub.PermissionId;
                    var hasAccess = sub.HasAccess;

                    if (permissionId is not null && hasAccess == false)
                    {
                        await _adminCompanyRepository.RemoveCompanyPermission(permissionId.Value);
                        await _adminCompanyRepository.RemoveCompanyPermission(model.Id, sub.Id);
                        permissionsUpdated = true;
                    }

                    if (permissionId is null && hasAccess == true)
                    {
                        var newPermission = sub.ToDomainPermission();
                        newPermission.CompanyId = model.Id;

                        await _adminCompanyRepository.AddCompanyPermission(newPermission);
                        permissionsUpdated = true;
                    }

                    if (permissionId is null && hasAccess == false)
                    {
                        await _adminCompanyRepository.RemoveCompanyPermission(model.Id, sub.Id);
                        permissionsUpdated = true;
                    }
                }
            }
        }

        if (permissionsUpdated)
            result.SuccessMessages.Add("Updated Company Permissions");

        if (jeevesCompaniesChanged)
            result.SuccessMessages.Add("Updated company ForetagKod configuration");

        if (companySettingsUpdated || permissionsUpdated || jeevesCompaniesChanged)
            _applicationMenuService.Invalidate(model.Id);

        return result;
    }

    private async Task<bool> HasJeevesCompanyConfigurationChangedAsync(ManageCompanyVM model)
    {
        var postedRows = NormalizeJeevesCompanyRows(model.JeevesCompanies);
        var existingRows = await _context.CompanyJeevesCompanies!
            .AsNoTracking()
            .Where(x => x.CompanyId == model.Id)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CompanyCode)
            .Select(x => new NormalizedJeevesCompanyRow(
                x.CompanyCode,
                (x.DisplayName ?? string.Empty).Trim(),
                x.IsDefault,
                x.IsActive,
                x.SortOrder))
            .ToListAsync();

        if (postedRows.Count != existingRows.Count)
            return true;

        for (var i = 0; i < postedRows.Count; i++)
        {
            if (!postedRows[i].Equals(existingRows[i]))
                return true;
        }

        return false;
    }

    private async Task<List<string>> SyncCompanyJeevesCompaniesAsync(ManageCompanyVM model)
    {
        var errors = new List<string>();
        var rows = (model.JeevesCompanies ?? new List<AdminCompanyJeevesCompanyViewModel>())
            .Where(x => x.CompanyCode.HasValue || !string.IsNullOrWhiteSpace(x.DisplayName))
            .ToList();

        foreach (var row in rows)
        {
            if (!row.CompanyCode.HasValue)
                errors.Add("Varje Jeeves-bolag måste ha en ForetagKod.");
            if (string.IsNullOrWhiteSpace(row.DisplayName))
                errors.Add("Varje Jeeves-bolag måste ha ett visningsnamn.");
        }

        if (errors.Count > 0)
            return errors;

        var duplicateCodes = rows
            .Where(x => x.CompanyCode.HasValue)
            .GroupBy(x => x.CompanyCode!.Value)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateCodes.Count > 0)
            errors.Add($"Dubbel ForetagKod hittades: {string.Join(", ", duplicateCodes)}");

        var activeDefaultCount = rows.Count(x => x.IsActive && x.IsDefault);
        if (rows.Count > 0 && activeDefaultCount != 1)
            errors.Add("Exakt ett aktivt Jeeves-bolag måste vara markerat som standard.");

        if (errors.Count > 0)
            return errors;

        var validationErrors = await ValidateCompanyCodesAgainstJeevesAsync(model.Id, rows);
        if (validationErrors.Count > 0)
        {
            errors.AddRange(validationErrors);
            return errors;
        }

        var existing = await _context.CompanyJeevesCompanies!
            .Where(x => x.CompanyId == model.Id)
            .ToListAsync();

        var existingById = existing.ToDictionary(x => x.Id);
        var keepIds = new HashSet<Guid>();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
            keepIds.Add(id);

            if (!existingById.TryGetValue(id, out var entity))
            {
                entity = new ApplicationCompanyJeevesCompany
                {
                    Id = id,
                    CompanyId = model.Id
                };
                _context.CompanyJeevesCompanies!.Add(entity);
            }

            entity.CompanyCode = row.CompanyCode!.Value;
            entity.DisplayName = row.DisplayName!.Trim();
            entity.IsDefault = row.IsDefault;
            entity.IsActive = row.IsActive;
            entity.SortOrder = row.SortOrder > 0 ? row.SortOrder : (i + 1);
        }

        var toRemove = existing.Where(x => !keepIds.Contains(x.Id)).ToList();
        if (toRemove.Count > 0)
            _context.CompanyJeevesCompanies!.RemoveRange(toRemove);

        var defaultCode = rows.FirstOrDefault(x => x.IsDefault && x.IsActive)?.CompanyCode;
        var companyEntity = await _context.Companies!.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (companyEntity != null)
            companyEntity.DefaultJeevesCompanyCode = defaultCode;

        await _context.SaveChangesAsync();
        model.DefaultJeevesCompanyCode = defaultCode;
        return errors;
    }

    private async Task<List<string>> ValidateCompanyCodesAgainstJeevesAsync(
        Guid companyId,
        IReadOnlyCollection<AdminCompanyJeevesCompanyViewModel> rows)
    {
        if (rows.Count == 0)
            return new List<string>();

        var activeConnection = await ResolveActiveCompanyConnectionAsync(companyId);
        if (!activeConnection.Success || string.IsNullOrWhiteSpace(activeConnection.ConnectionString))
        {
            return new List<string>
            {
                activeConnection.Error
                ?? "Kunde inte verifiera ForetagKod eftersom bolaget saknar en aktiv Jeeves-anslutning."
            };
        }

        const string validationSql = @"
SELECT CAST(CASE WHEN
    EXISTS (SELECT 1 FROM dbo.fr WHERE ForetagKod = @CompanyCode)
    OR EXISTS (SELECT 1 FROM dbo.oh WHERE ForetagKod = @CompanyCode)
    OR EXISTS (SELECT 1 FROM dbo.fh WHERE ForetagKod = @CompanyCode)
THEN 1 ELSE 0 END AS bit);";

        var databaseLabel = string.IsNullOrWhiteSpace(activeConnection.DatabaseName)
            ? "aktiv Jeeves-databas"
            : activeConnection.DatabaseName;

        var errors = new List<string>();
        foreach (var companyCode in rows
                     .Where(x => x.CompanyCode.HasValue)
                     .Select(x => x.CompanyCode!.Value)
                     .Distinct()
                     .OrderBy(x => x))
        {
            bool? exists;
            try
            {
                exists = await _jeevesSqlExecutor.ExecuteScalarAsync<bool?>(
                    activeConnection.ConnectionString!,
                    validationSql,
                    new { CompanyCode = companyCode },
                    operationName: "AdminCompanyManagementService.ValidateCompanyCode",
                    commandTimeoutSeconds: 10);
            }
            catch (Exception ex)
            {
                errors.Add(
                    $"Kunde inte verifiera ForetagKod mot {databaseLabel}: {ex.Message}");
                return errors;
            }

            if (exists != true)
            {
                errors.Add(
                    $"ForetagKod {companyCode} kunde inte verifieras i {databaseLabel}. Kontrollera att koden finns i kundens Jeeves-data eller välj rätt aktiv anslutning.");
            }
        }

        return errors;
    }

    private static List<NormalizedJeevesCompanyRow> NormalizeJeevesCompanyRows(
        IReadOnlyCollection<AdminCompanyJeevesCompanyViewModel>? rows)
    {
        return (rows ?? Array.Empty<AdminCompanyJeevesCompanyViewModel>())
            .Where(x => x.CompanyCode.HasValue || !string.IsNullOrWhiteSpace(x.DisplayName))
            .Select((x, index) => new NormalizedJeevesCompanyRow(
                x.CompanyCode ?? 0,
                (x.DisplayName ?? string.Empty).Trim(),
                x.IsDefault,
                x.IsActive,
                x.SortOrder > 0 ? x.SortOrder : (index + 1)))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CompanyCode)
            .ToList();
    }

    private readonly record struct NormalizedJeevesCompanyRow(
        int CompanyCode,
        string DisplayName,
        bool IsDefault,
        bool IsActive,
        int SortOrder);

    private async Task<(bool Success, string? ConnectionString, string? DatabaseName, string? Error)> ResolveActiveCompanyConnectionAsync(Guid companyId)
    {
        var connStrings = await _applicationConnectionContextService.GetConnectionStringsAsync(_context, companyId);
        var active = connStrings.FirstOrDefault(x => x.IsActive) ?? connStrings.FirstOrDefault();
        if (active is null)
        {
            return (false, null, null, "Kunde inte verifiera ForetagKod eftersom bolaget saknar en aktiv Jeeves-anslutning i portalen.");
        }

        var resolved = await _connectionStringResolver.ResolveAsync(connStrings, active.Id, companyId);
        if (!resolved.Success)
        {
            return (false, null, active.DatabaseName, resolved.Error ?? "Kunde inte lösa bolagets aktiva Jeeves-anslutning.");
        }

        return (true, resolved.Value, active.DatabaseName, null);
    }

    private async Task<ManageCompanyVM> RehydrateManageCompanyForErrorsAsync(ManageCompanyVM model)
    {
        var hydrated = await GetManageCompanyAsync(model.Id) ?? model;
        hydrated.Name = model.Name;
        hydrated.DefaultJeevesCompanyCode = model.DefaultJeevesCompanyCode;
        hydrated.JeevesCompanies = (model.JeevesCompanies ?? new List<AdminCompanyJeevesCompanyViewModel>()).ToList();

        if (model.AllModules is not null && model.AllModules.Count > 0)
        {
            hydrated.AllModules = model.AllModules;
            FilterHiddenIntegrationSubModules(hydrated);
        }

        return hydrated;
    }

    private static void FilterHiddenIntegrationSubModules(ManageCompanyVM model)
    {
        if (model.AllModules is null || model.AllModules.Count == 0)
            return;

        var hiddenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Ongoing",
            "Jobs",
            "Config"
        };

        foreach (var module in model.AllModules)
        {
            if (module.SubModules is null || module.SubModules.Count == 0)
                continue;

            module.SubModules = module.SubModules
                .Where(subModule => !hiddenNames.Contains(subModule.Name ?? string.Empty))
                .ToList();
        }
    }

    private async Task<OperationResult<string>> ResolveCompanyConnectionAsync(Guid companyId, Guid connectionStringId)
    {
        var connStrings = await _applicationConnectionContextService.GetConnectionStringsAsync(_context, companyId);
        return await _connectionStringResolver.ResolveAsync(connStrings, connectionStringId, companyId);
    }

    private static bool ValidatorTryValidate(AdminCreateCompanyViewModel model)
    {
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        return System.ComponentModel.DataAnnotations.Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);
    }
}

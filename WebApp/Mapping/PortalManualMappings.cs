using Entities.Application;
using Entities.ViewModels.Admin;
using WebApp.Models.Identity;

namespace WebApp.Mapping;

// Centralized manual mappings for the active portal code path.
// Keep these mappings explicit so security-critical admin/session models do not
// rely on hidden convention-based property copying.
public static class PortalManualMappings
{
    public static Company ToDomainCompany(this ApplicationCompany source)
    {
        return new Company
        {
            Id = source.Id,
            Name = source.Name ?? string.Empty,
            DefaultJeevesCompanyCode = source.DefaultJeevesCompanyCode,
            // Company/session flows still need the configured connection metadata.
            ConnectionStrings = source.ConnectionStrings?.Select(ToDomainConnectionString).ToList()
        };
    }

    public static Company ToDomainCompany(this ManageCompanyVM source)
    {
        return new Company
        {
            Id = source.Id,
            Name = source.Name ?? string.Empty,
            DefaultJeevesCompanyCode = source.DefaultJeevesCompanyCode
        };
    }

    public static CompanyPermission ToDomainPermission(this AdminSubModuleViewModel source)
    {
        return new CompanyPermission
        {
            ModuleId = source.ModuleId,
            // The view model Id represents the selected submodule.
            SubModuleId = source.Id
        };
    }

    public static AdminUserViewModel ToAdminUserViewModel(this ApplicationUser source)
    {
        return new AdminUserViewModel
        {
            UserId = source.Id,
            UserName = source.UserName ?? string.Empty,
            FirstName = source.FirstName ?? string.Empty,
            LastName = source.LastName ?? string.Empty,
            Email = source.Email ?? string.Empty,
            PersSign = source.PersSign ?? string.Empty,
            EmailValidated = source.EmailConfirmed,
            CompanyId = source.CompanyId ?? Guid.Empty,
            ActiveConnectionStringId = source.ActiveConnectionStringId,
            PhoneNumber = source.PhoneNumber,
            ProfilePicture = source.ProfilePicture
        };
    }

    public static CompanyLicense ToDomainLicense(this ApplicationCompanyLicense source)
    {
        return new CompanyLicense
        {
            Id = source.Id,
            CompanyId = source.CompanyId,
            ZeeuProductId = source.ZeeuProductId,
            Enabled = source.Enabled
        };
    }

    public static CompanyPermission ToDomainPermission(this ApplicationCompanyPermission source)
    {
        return new CompanyPermission
        {
            Id = source.Id,
            CompanyId = source.CompanyId,
            ModuleId = source.ModuleId,
            SubModuleId = source.SubModuleId
        };
    }
    private static CompanyConnectionString ToDomainConnectionString(ApplicationCompanyConnectionStrings source)
    {
        return new CompanyConnectionString
        {
            Id = source.Id,
            CompanyId = source.CompanyId,
            ConnectionStringTypeId = source.ConnectionStringTypeId,
            DatabaseName = source.DatabaseName,
            IsActive = source.IsActive
        };
    }
}

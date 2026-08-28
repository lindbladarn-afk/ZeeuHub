using Entities.Application;
using WebApp.Data;
using WebApp.Mapping;
using WebApp.Models.Identity;

namespace WebApp.Services.Application;

public class CompanyBuilder : ICompanyBuilder
{
    private readonly IApplicationCompanyContextService _companyContextService;

    public CompanyBuilder(IApplicationCompanyContextService companyContextService)
    {
        _companyContextService = companyContextService;
    }

    public async Task<Company> BuildAsync(ApplicationCompany applicationCompany, ApplicationDbContext context)
    {
        var company = applicationCompany.ToDomainCompany();
        company.Licenses = (await _companyContextService.GetLicensesAsync(context, applicationCompany.Id))
            .Select(x => x.ToDomainLicense())
            .ToList();
        company.Permissions = (await _companyContextService.GetPermissionsAsync(context, applicationCompany.Id))
            .Select(x => x.ToDomainPermission())
            .ToList();
        return company;
    }
}

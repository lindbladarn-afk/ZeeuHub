namespace Repository.Contracts;

public interface IAdminRepository
{
    Task<int> GetCompanyCountAsync();
    Task<List<ManageCompanyVM>> GetCompanies();
    Task<ManageCompanyVM?> GetCompanyByIdAsync(Guid CompanyId);
    Task<IEnumerable<AdminAllCompaniesForSelectListVM>> GetAllCompaniesForSelectList();
    Task<IEnumerable<AdminCompanyConnectionStringViewModel>> GetCompanyConnectionStringsForSelectListAsync(Guid? companyId);
    Task<IEnumerable<AdminCompanyConnectionStringTypeViewModel>> GetConnectionStringTypesAsync();
    Task<Guid> CreateCompanyAsync(AdminCreateCompanyViewModel model);
    Task<ManageCompanyVM?> GetUserCompany(string userId);
    Task<IEnumerable<UserCompanyLookup>> GetUserCompaniesLookup();
    Task UpdateCompanyAsync(Company company);
    Task RemoveCompanyPermission(Guid permissionId);
    Task RemoveCompanyPermission(Guid companyId, Guid subModuleId);
    Task AddCompanyPermission(CompanyPermission companyPermission);
}

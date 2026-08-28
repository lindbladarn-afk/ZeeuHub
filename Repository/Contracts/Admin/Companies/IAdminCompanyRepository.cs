namespace Repository.Contracts;

public interface IAdminCompanyRepository
{
    Task<int> GetCompanyCountAsync();
    Task<List<ManageCompanyVM>> GetCompanies();
    Task<ManageCompanyVM?> GetCompanyByIdAsync(Guid companyId);
    Task<IEnumerable<AdminAllCompaniesForSelectListVM>> GetAllCompaniesForSelectList();
    Task<IEnumerable<AdminCompanyConnectionStringViewModel>> GetCompanyConnectionStringsForSelectListAsync(Guid? companyId);
    Task<IEnumerable<AdminCompanyConnectionStringTypeViewModel>> GetConnectionStringTypesAsync();
    Task<Guid> CreateCompanyAsync(AdminCreateCompanyViewModel model);
    Task UpdateCompanyAsync(Company company);
    Task RemoveCompanyPermission(Guid permissionId);
    Task RemoveCompanyPermission(Guid companyId, Guid subModuleId);
    Task AddCompanyPermission(CompanyPermission companyPermission);
}

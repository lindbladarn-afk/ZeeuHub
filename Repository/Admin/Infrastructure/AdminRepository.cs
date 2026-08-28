namespace Repository;

public class AdminRepository : IAdminRepository
{
    private readonly IAdminCompanyRepository _adminCompanyRepository;
    private readonly IAdminUserLookupRepository _adminUserLookupRepository;

    public AdminRepository(
        IAdminCompanyRepository adminCompanyRepository,
        IAdminUserLookupRepository adminUserLookupRepository)
    {
        _adminCompanyRepository = adminCompanyRepository;
        _adminUserLookupRepository = adminUserLookupRepository;
    }

    public Task<int> GetCompanyCountAsync() => _adminCompanyRepository.GetCompanyCountAsync();

    public Task<List<ManageCompanyVM>> GetCompanies() => _adminCompanyRepository.GetCompanies();

    public Task<ManageCompanyVM?> GetCompanyByIdAsync(Guid companyId) => _adminCompanyRepository.GetCompanyByIdAsync(companyId);

    public Task<IEnumerable<AdminAllCompaniesForSelectListVM>> GetAllCompaniesForSelectList() => _adminCompanyRepository.GetAllCompaniesForSelectList();

    public Task<IEnumerable<AdminCompanyConnectionStringViewModel>> GetCompanyConnectionStringsForSelectListAsync(Guid? companyId) =>
        _adminCompanyRepository.GetCompanyConnectionStringsForSelectListAsync(companyId);

    public Task<IEnumerable<AdminCompanyConnectionStringTypeViewModel>> GetConnectionStringTypesAsync() => _adminCompanyRepository.GetConnectionStringTypesAsync();

    public Task<Guid> CreateCompanyAsync(AdminCreateCompanyViewModel model) => _adminCompanyRepository.CreateCompanyAsync(model);

    public Task<ManageCompanyVM?> GetUserCompany(string userId) => _adminUserLookupRepository.GetUserCompany(userId);

    public Task<IEnumerable<UserCompanyLookup>> GetUserCompaniesLookup() => _adminUserLookupRepository.GetUserCompaniesLookup();

    public Task UpdateCompanyAsync(Company company) => _adminCompanyRepository.UpdateCompanyAsync(company);

    public Task RemoveCompanyPermission(Guid permissionId) => _adminCompanyRepository.RemoveCompanyPermission(permissionId);

    public Task RemoveCompanyPermission(Guid companyId, Guid subModuleId) =>
        _adminCompanyRepository.RemoveCompanyPermission(companyId, subModuleId);

    public Task AddCompanyPermission(CompanyPermission companyPermission) => _adminCompanyRepository.AddCompanyPermission(companyPermission);
}

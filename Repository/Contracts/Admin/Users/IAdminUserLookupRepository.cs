namespace Repository.Contracts;

public interface IAdminUserLookupRepository
{
    Task<ManageCompanyVM?> GetUserCompany(string userId);
    Task<IEnumerable<UserCompanyLookup>> GetUserCompaniesLookup();
}

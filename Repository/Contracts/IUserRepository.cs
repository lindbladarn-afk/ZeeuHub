namespace Repository.Contracts;

public interface IUserRepository
{
    Task<IEnumerable<JeevesCompanyVM>> GetJeevesCompaniesAsync(string connectionString, string persSign);
}

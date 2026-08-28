using Entities.Application;
using Entities.User;

namespace WebApp.Services.Application;

public interface IJeevesCompanyAccessService
{
    Task<IReadOnlyList<JeevesCompanyVM>> GetCompaniesAsync(UserSession? sessionUser, CancellationToken cancellationToken = default);
    Task<bool> HasCompanyAccessAsync(UserSession? sessionUser, int companyCode, CancellationToken cancellationToken = default);
    Task<int?> ResolveCompanyCodeAsync(UserSession? sessionUser, CancellationToken cancellationToken = default);
    Task<string> ResolveCompanyNameAsync(UserSession? sessionUser, int? companyCode, CancellationToken cancellationToken = default);
    void Store(UserSession? sessionUser, IReadOnlyList<JeevesCompanyVM> companies);
}

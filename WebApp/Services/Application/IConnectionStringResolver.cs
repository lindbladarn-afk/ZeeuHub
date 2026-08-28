using WebApp.Helpers;
using WebApp.Models.Identity;

namespace WebApp.Services.Application;

public interface IConnectionStringResolver
{
    Task<OperationResult<string>> ResolveAsync(IEnumerable<ApplicationCompanyConnectionStrings> companyConnectionStrings, Guid activeConnectionStringId, Guid companyId);
}

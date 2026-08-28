using Entities.Application;
using WebApp.Helpers;

namespace WebApp.Services.Application;

public interface ITenantGuard
{
    OperationResult<bool> Validate(UserSession? session, int? requestedCompanyCode = null);
}

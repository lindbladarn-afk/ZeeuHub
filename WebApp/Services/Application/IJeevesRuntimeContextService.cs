using Entities.Application;
using WebApp.Helpers;

namespace WebApp.Services.Application;

public interface IJeevesRuntimeContextService
{
    Task<OperationResult<JeevesRuntimeContext>> ResolveAsync(UserSession? sessionUser, CancellationToken cancellationToken = default);
}

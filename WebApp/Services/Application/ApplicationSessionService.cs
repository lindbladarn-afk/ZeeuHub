using Entities.Application;
using Microsoft.AspNetCore.Http;

namespace WebApp.Services.Application;

public class ApplicationSessionService : IApplicationSessionService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ApplicationSessionService> _logger;

    public ApplicationSessionService(IHttpContextAccessor httpContextAccessor, ILogger<ApplicationSessionService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public bool TrySetUserSession(UserSession sessionUser)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            _logger.LogError("HttpContext is null when setting session");
            return false;
        }

        httpContext.Session.Set("UserObject", sessionUser);
        return true;
    }
}

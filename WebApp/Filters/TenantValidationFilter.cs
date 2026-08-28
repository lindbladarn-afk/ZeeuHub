using Entities.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebApp.Services;
using WebApp.Services.Application;

namespace WebApp.Filters;

public class TenantValidationFilter : IAsyncActionFilter
{
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly ITenantGuard _tenantGuard;

    public TenantValidationFilter(IHttpContextAccessor contextAccessor, ITenantGuard tenantGuard)
    {
        _contextAccessor = contextAccessor;
        _tenantGuard = tenantGuard;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var sessionUser = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
        var validation = _tenantGuard.Validate(sessionUser);

        if (!validation.Success)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}

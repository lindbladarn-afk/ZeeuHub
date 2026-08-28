using Entities.Application;
using Microsoft.AspNetCore.Authorization;
using WebApp.Services;

namespace WebApp.Services.ControlPanel;

// Bridges the Control Panel access service into ASP.NET authorization.
public sealed class ControlPanelAccessHandler : AuthorizationHandler<ControlPanelAccessRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IControlPanelAccessService _accessService;

    public ControlPanelAccessHandler(
        IHttpContextAccessor httpContextAccessor,
        IControlPanelAccessService accessService)
    {
        _httpContextAccessor = httpContextAccessor;
        _accessService = accessService;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ControlPanelAccessRequirement requirement)
    {
        var sessionUser = _httpContextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
        if (_accessService.IsAuthorizedTenant(sessionUser))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

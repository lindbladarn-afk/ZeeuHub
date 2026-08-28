using System.Security.Claims;
using Entities.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using WebApp.Models.Identity;
using WebApp.Services;
using WebApp.Services.Application;

namespace WebApp.Middleware;

public sealed class UserSessionBootstrapMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserSessionBootstrapMiddleware> _logger;

    public UserSessionBootstrapMiddleware(RequestDelegate next, ILogger<UserSessionBootstrapMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IUserSessionBootstrapService userSessionBootstrapService,
        IOptions<PortalAuthenticationOptions> authenticationOptions)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var sessionUser = context.Session.Get<UserSession>("UserObject");

            if (sessionUser == null)
            {
                var email =
                    context.User.FindFirstValue(ClaimTypes.Email) ??
                    context.User.FindFirstValue(ClaimTypes.Name);

                if (!string.IsNullOrWhiteSpace(email))
                {
                    var restored = await userSessionBootstrapService.AddUserToSessionAsync(email);
                    if (!restored)
                    {
                        _logger.LogWarning("Failed to bootstrap user session for authenticated user {Email}", email);

                        context.Session.Clear();
                        if (authenticationOptions.Value.SignOutWhenSessionBootstrapFails && !context.Response.HasStarted)
                        {
                            await context.SignOutAsync(IdentityConstants.ApplicationScheme);

                            var returnUrl = $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
                            var encodedReturnUrl = Uri.EscapeDataString(returnUrl);
                            context.Response.Redirect($"/Identity/Account/Login?returnUrl={encodedReturnUrl}");
                            return;
                        }
                    }
                }
            }
        }

        await _next(context);
    }
}

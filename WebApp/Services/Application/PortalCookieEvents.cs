using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace WebApp.Services.Application;

public sealed class PortalCookieEvents : CookieAuthenticationEvents
{
    private readonly ILogger<PortalCookieEvents> _logger;

    public PortalCookieEvents(ILogger<PortalCookieEvents> logger)
    {
        _logger = logger;
    }

    public override Task SigningIn(CookieSigningInContext context)
    {
        context.HttpContext.Session.Clear();
        return base.SigningIn(context);
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal?.FindFirstValue(ClaimTypes.Email) ??
                    principal?.FindFirstValue(ClaimTypes.Name);

        if (!string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(email))
        {
            await base.ValidatePrincipal(context);
            return;
        }

        _logger.LogWarning(
            "Rejecting application cookie because required identity claims are missing. UserId present: {HasUserId}, email present: {HasEmail}",
            !string.IsNullOrWhiteSpace(userId),
            !string.IsNullOrWhiteSpace(email));

        context.RejectPrincipal();
        context.HttpContext.Session.Clear();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }

    public override Task SigningOut(CookieSigningOutContext context)
    {
        context.HttpContext.Session.Clear();
        return base.SigningOut(context);
    }
}

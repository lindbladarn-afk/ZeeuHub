using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using WebApp.Models.Identity;

namespace WebApp.Services.ActionCenter;

public sealed class IdentityActionCenterSeenStore : IActionCenterSeenStore
{
    private const string ClaimType = "zeeu.actioncenter.lastseen.utc";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityActionCenterSeenStore(IHttpContextAccessor httpContextAccessor, UserManager<ApplicationUser> userManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
    }

    public async Task<DateTime?> GetLastSeenUtcAsync(CancellationToken cancellationToken)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal == null || !(principal.Identity?.IsAuthenticated ?? false))
            return null;

        var user = await _userManager.GetUserAsync(principal);
        if (user == null)
            return null;

        var claims = await _userManager.GetClaimsAsync(user);
        var value = claims.FirstOrDefault(c => c.Type == ClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return dt.ToUniversalTime();

        return null;
    }

    public async Task SetLastSeenUtcAsync(DateTime utcTimestamp, CancellationToken cancellationToken)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal == null || !(principal.Identity?.IsAuthenticated ?? false))
            return;

        var user = await _userManager.GetUserAsync(principal);
        if (user == null)
            return;

        var claims = await _userManager.GetClaimsAsync(user);
        var existing = claims.FirstOrDefault(c => c.Type == ClaimType);
        var newValue = utcTimestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        if (existing != null)
        {
            await _userManager.ReplaceClaimAsync(user, existing, new System.Security.Claims.Claim(ClaimType, newValue));
            return;
        }

        await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim(ClaimType, newValue));
    }
}

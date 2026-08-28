using WebApp.Services;
using Entities.Application;
using WebApp.Services.Telemetry;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace WebApp.Middleware;

public class PortalSessionMiddleware
{
    private const string LastSeenKey = "PortalUsageLastSeenTicks";
    private readonly RequestDelegate _next;
    private readonly ILogger<PortalSessionMiddleware> _logger;

    public PortalSessionMiddleware(RequestDelegate next, ILogger<PortalSessionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITelemetryService telemetryService)
    {
        try
        {
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var sessionUser = context.Session.Get<UserSession>("UserObject");
                if (sessionUser != null)
                {
                    if (!string.IsNullOrWhiteSpace(sessionUser.Language))
                    {
                        try
                        {
                            var culture = new CultureInfo(sessionUser.Language);
                            CultureInfo.CurrentCulture = culture;
                            CultureInfo.CurrentUICulture = culture;
                            context.Features.Set<IRequestCultureFeature>(new RequestCultureFeature(new RequestCulture(culture), new AcceptLanguageHeaderRequestCultureProvider()));
                        }
                        catch (CultureNotFoundException)
                        {
                            // ignore invalid culture codes in session
                        }
                    }

                    if (sessionUser.CompanyId is null)
                    {
                        // We only log usage when a company is set; guard to avoid DB errors on required CompanyId.
                        await _next(context);
                        return;
                    }

                    var now = DateTime.UtcNow;
                    var stored = context.Session.GetString(LastSeenKey);
                    DateTime? lastSeen = null;
                    if (long.TryParse(stored, out var ticks) && ticks > 0)
                    {
                        lastSeen = new DateTime(ticks, DateTimeKind.Utc);
                    }

                    if (lastSeen is null)
                    {
                        context.Session.SetString(LastSeenKey, now.Ticks.ToString());
                        // Skapa/posta första raden direkt så "senast inloggad" uppdateras.
                        await telemetryService.AddUsageAsync(sessionUser.UserId, sessionUser.CompanyId, 0, now, ensureRecord: true);
                    }
                    else
                    {
                        var diffMinutes = (int)Math.Floor((now - lastSeen.Value).TotalMinutes);
                        if (diffMinutes > 0)
                        {
                            // Cap increments to avoid runaway if session is resumed after a long break.
                            var increment = Math.Min(diffMinutes, 60);
                            await telemetryService.AddUsageAsync(sessionUser.UserId, sessionUser.CompanyId, increment, now);
                            context.Session.SetString(LastSeenKey, now.Ticks.ToString());
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update portal session telemetry");
        }

        await _next(context);
    }
}

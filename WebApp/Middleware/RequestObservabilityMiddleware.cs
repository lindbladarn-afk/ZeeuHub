using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Entities.Application;
using WebApp.Observability;
using WebApp.Services;

namespace WebApp.Middleware;

// Adds safe request, tenant, and support context to every downstream log entry.
public sealed partial class RequestObservabilityMiddleware
{
    private const int MaxCorrelationIdLength = 128;
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestObservabilityMiddleware> _logger;
    private readonly string _environment;
    private readonly string _releaseVersion;

    public RequestObservabilityMiddleware(
        RequestDelegate next,
        ILogger<RequestObservabilityMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment.EnvironmentName;
        _releaseVersion = ResolveReleaseVersion();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        var supportId = ResolveSupportId(context, traceId);
        var sessionUser = TryGetSessionUser(context);
        var userId = sessionUser?.UserId
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var module = context.Request.RouteValues["controller"]?.ToString()
            ?? context.Request.RouteValues["page"]?.ToString()
            ?? "Unknown";
        var operation = context.Request.RouteValues["action"]?.ToString()
            ?? context.GetEndpoint()?.DisplayName
            ?? "Unknown";

        context.Items[PortalObservability.CorrelationIdItemKey] = correlationId;
        context.Items[PortalObservability.SupportIdItemKey] = supportId;
        context.Items[PortalObservability.CompanyIdItemKey] = sessionUser?.CompanyId;
        context.Items[PortalObservability.JeevesCompanyCodeItemKey] = sessionUser?.JeevesActiveCompany;
        context.Items[PortalObservability.UserIdItemKey] = userId;
        context.Items[PortalObservability.ModuleItemKey] = module;
        context.Items[PortalObservability.OperationItemKey] = operation;
        context.Response.Headers[PortalObservability.CorrelationHeaderName] = correlationId;
        context.Response.Headers[PortalObservability.SupportHeaderName] = supportId;

        AddActivityTags(
            correlationId,
            supportId,
            sessionUser?.CompanyId,
            sessionUser?.JeevesActiveCompany,
            userId,
            module,
            operation);

        var scope = new Dictionary<string, object?>
        {
            ["TraceId"] = traceId,
            ["CorrelationId"] = correlationId,
            ["SupportId"] = supportId,
            ["CompanyId"] = sessionUser?.CompanyId,
            ["JeevesCompanyCode"] = sessionUser?.JeevesActiveCompany,
            ["UserId"] = userId,
            ["Module"] = module,
            ["Operation"] = operation,
            ["Environment"] = _environment,
            ["ReleaseVersion"] = _releaseVersion,
            ["HttpMethod"] = context.Request.Method,
            ["RequestPath"] = context.Request.Path.Value
        };

        using (_logger.BeginScope(scope))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var incoming = context.Request.Headers[PortalObservability.CorrelationHeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(incoming))
        {
            incoming = incoming.Trim();
            if (incoming.Length <= MaxCorrelationIdLength && SafeCorrelationIdRegex().IsMatch(incoming))
            {
                return incoming;
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    private static string ResolveSupportId(HttpContext context, string traceId)
    {
        if (context.Items.TryGetValue(PortalObservability.SupportIdItemKey, out var existing) &&
            existing is string existingSupportId &&
            !string.IsNullOrWhiteSpace(existingSupportId))
        {
            return existingSupportId;
        }

        var normalizedTraceId = new string(traceId.Where(char.IsAsciiHexDigit).ToArray());
        return normalizedTraceId.Length >= 8
            ? normalizedTraceId[..8].ToLowerInvariant()
            : Guid.NewGuid().ToString("N")[..8];
    }

    private static UserSession? TryGetSessionUser(HttpContext context)
    {
        try
        {
            return context.Session.Get<UserSession>("UserObject");
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static void AddActivityTags(
        string correlationId,
        string supportId,
        Guid? companyId,
        int? jeevesCompanyCode,
        string? userId,
        string module,
        string operation)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        activity.SetTag("portal.correlation_id", correlationId);
        activity.SetTag("portal.support_id", supportId);
        activity.SetTag("portal.company_id", companyId?.ToString("D"));
        activity.SetTag("portal.jeeves_company_code", jeevesCompanyCode);
        activity.SetTag("enduser.id", userId);
        activity.SetTag("portal.module", module);
        activity.SetTag("portal.operation", operation);
    }

    private static string ResolveReleaseVersion()
        => Assembly.GetEntryAssembly()?
               .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
               .InformationalVersion
           ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
           ?? "unknown";

    [GeneratedRegex("^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeCorrelationIdRegex();
}

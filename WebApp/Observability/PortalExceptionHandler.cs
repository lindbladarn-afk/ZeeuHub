using System.Diagnostics;
using System.Net.Mime;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.Observability;

// Converts unhandled failures into safe client responses and one structured error log.
public sealed class PortalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<PortalExceptionHandler> _logger;

    public PortalExceptionHandler(ILogger<PortalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var supportId = GetOrCreateSupportId(httpContext);
        var correlationId = httpContext.Items[PortalObservability.CorrelationIdItemKey]?.ToString()
            ?? httpContext.TraceIdentifier;

        _logger.LogError(
            exception,
            "Unhandled portal exception. {ErrorCode} {ExceptionType} {SupportId} {CorrelationId} {CompanyId} {JeevesCompanyCode} {UserId} {Module} {Operation}",
            PortalErrorCodes.UnhandledException,
            exception.GetType().FullName,
            supportId,
            correlationId,
            httpContext.Items[PortalObservability.CompanyIdItemKey],
            httpContext.Items[PortalObservability.JeevesCompanyCodeItemKey],
            httpContext.Items[PortalObservability.UserIdItemKey],
            httpContext.Items[PortalObservability.ModuleItemKey],
            httpContext.Items[PortalObservability.OperationItemKey]);

        httpContext.Response.Clear();
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.Headers[PortalObservability.CorrelationHeaderName] = correlationId;
        httpContext.Response.Headers[PortalObservability.SupportHeaderName] = supportId;

        if (ExpectsJson(httpContext.Request))
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ett oväntat fel uppstod.",
                Detail = $"Försök igen eller kontakta support. Referens: {supportId}",
                Instance = httpContext.Request.Path
            };
            problem.Extensions["supportId"] = supportId;
            problem.Extensions["correlationId"] = correlationId;

            httpContext.Response.ContentType = MediaTypeNames.Application.ProblemJson;
            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            return true;
        }

        var encodedSupportId = HtmlEncoder.Default.Encode(supportId);
        httpContext.Response.ContentType = MediaTypeNames.Text.Html;
        await httpContext.Response.WriteAsync(
            $"""
            <!doctype html>
            <html lang="sv">
            <head><meta charset="utf-8"><title>Ett fel uppstod</title></head>
            <body>
              <main>
                <h1>Något gick fel</h1>
                <p>Försök igen eller kontakta support.</p>
                <p>Referens: <code>{encodedSupportId}</code></p>
              </main>
            </body>
            </html>
            """,
            cancellationToken);

        return true;
    }

    private static bool ExpectsJson(HttpRequest request)
        => request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
           || request.GetTypedHeaders().Accept?.Any(value =>
               value.MediaType.Value?.Contains("json", StringComparison.OrdinalIgnoreCase) == true) == true;

    private static string GetOrCreateSupportId(HttpContext context)
    {
        if (context.Items[PortalObservability.SupportIdItemKey] is string supportId &&
            !string.IsNullOrWhiteSpace(supportId))
        {
            return supportId;
        }

        var traceId = Activity.Current?.TraceId.ToString();
        supportId = !string.IsNullOrWhiteSpace(traceId) && traceId.Length >= 8
            ? traceId[..8]
            : Guid.NewGuid().ToString("N")[..8];
        context.Items[PortalObservability.SupportIdItemKey] = supportId;
        return supportId;
    }
}

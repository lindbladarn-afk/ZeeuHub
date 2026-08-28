using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebApp.Observability;
using WebApp.Services.Integration;

namespace WebApp.Services.Integration.BankReconciliation;

// Formats bank reconciliation errors for logs and user-facing fallbacks.
internal static class BankReconciliationErrorHandling
{
    internal static string BuildSupportId(HttpContext? context)
    {
        var supportId = context?.Items[PortalObservability.SupportIdItemKey]?.ToString();
        if (!string.IsNullOrWhiteSpace(supportId))
        {
            return supportId!;
        }

        supportId = Guid.NewGuid().ToString("N")[..8];
        if (context is not null)
        {
            context.Items[PortalObservability.SupportIdItemKey] = supportId;
        }

        return supportId;
    }

    internal static string LogAndBuildUserMessage(
        ILogger logger,
        HttpContext? context,
        string operation,
        string fallbackMessage,
        Exception exception)
        => LogDiagnosticAndBuildUserMessage(
            logger,
            context,
            operation,
            fallbackMessage,
            exception.Message);

    internal static string LogDiagnosticAndBuildUserMessage(
        ILogger logger,
        HttpContext? context,
        string operation,
        string fallbackMessage,
        string? diagnostic)
    {
        var supportId = BuildSupportId(context);
        logger.LogError(
            "{Operation} failed. SupportId={SupportId} {Diagnostic}",
            operation,
            supportId,
            IntegrationLogSanitizer.Diagnostic(diagnostic));

        return $"{fallbackMessage} Referens: {supportId}.";
    }
}

using System.Text;
using Entities.Mail;
using MailService;
using Microsoft.Extensions.Options;
using WebApp.Models.Application;

namespace WebApp.Services.Application;

public sealed class TechnicalErrorNotificationService : ITechnicalErrorNotificationService
{
    private readonly IMailManager _mailManager;
    private readonly IPortalEventLogService _portalEventLogService;
    private readonly IOptions<TechnicalNotificationOptions> _options;
    private readonly ILogger<TechnicalErrorNotificationService> _logger;

    public TechnicalErrorNotificationService(
        IMailManager mailManager,
        IPortalEventLogService portalEventLogService,
        IOptions<TechnicalNotificationOptions> options,
        ILogger<TechnicalErrorNotificationService> logger)
    {
        _mailManager = mailManager;
        _portalEventLogService = portalEventLogService;
        _options = options;
        _logger = logger;
    }

    public async Task NotifyAsync(TechnicalErrorNotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = _options.Value;
        if (!options.Enabled)
            return;

        var toRecipients = SplitRecipients(options.To);
        var ccRecipients = SplitRecipients(options.Cc);
        var bccRecipients = SplitRecipients(options.Bcc);

        if (toRecipients.Count == 0)
        {
            _logger.LogWarning(
                "Skipping technical error notification for module {Module} because no recipients are configured.",
                request.Module);
            return;
        }

        var moduleLabel = string.IsNullOrWhiteSpace(request.Module) ? "UnknownModule" : request.Module.Trim();
        var header = string.IsNullOrWhiteSpace(request.Header)
            ? $"Issue in {moduleLabel}"
            : request.Header.Trim();

        await _portalEventLogService.RecordAsync(
            new PortalEventLogEntry
            {
                OccurredAtUtc = DateTime.UtcNow,
                Module = moduleLabel,
                Action = header,
                CompanyId = request.CompanyId,
                JeevesCompanyCode = request.JeevesCompanyCode,
                UserId = request.UserId,
                UserEmail = request.UserEmail,
                RequestPath = request.RequestPath,
                CorrelationId = null,
                Severity = "Error",
                Message = string.IsNullOrWhiteSpace(request.Summary) ? header : request.Summary.Trim(),
                AdditionalData = request.Details,
                Exception = request.Exception
            },
            cancellationToken);

        var body = BuildBody(request);
        var mail = new MailModel
        {
            To = toRecipients[0],
            Subject = $"{options.SubjectPrefix}: {moduleLabel}",
            Header = header,
            Text = body,
            ErrorMessage = request.Exception?.ToString()
        };

        try
        {
            await _mailManager.SendNotificationMailAsync(
                mail,
                htmlOverride: null,
                toRecipients: toRecipients,
                ccRecipients: ccRecipients,
                bccRecipients: bccRecipients);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send technical error notification for module {Module}.",
                moduleLabel);
        }
    }

    private static string BuildBody(TechnicalErrorNotificationRequest request)
    {
        var body = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(request.Summary))
            body.AppendLine(request.Summary.Trim());

        if (!string.IsNullOrWhiteSpace(request.Details))
        {
            if (body.Length > 0)
                body.AppendLine();

            body.AppendLine(request.Details.Trim());
        }

        AppendContextLine(body, "CompanyId", request.CompanyId?.ToString("D"));
        AppendContextLine(body, "JeevesCompanyCode", request.JeevesCompanyCode?.ToString());
        AppendContextLine(body, "UserId", request.UserId);
        AppendContextLine(body, "UserEmail", request.UserEmail);
        AppendContextLine(body, "RequestPath", request.RequestPath);

        return body.ToString().Trim();
    }

    private static void AppendContextLine(StringBuilder body, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (body.Length > 0)
            body.AppendLine();

        body.Append(key)
            .Append(": ")
            .AppendLine(value.Trim());
    }

    private static IReadOnlyList<string> SplitRecipients(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

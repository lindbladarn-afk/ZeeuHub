using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Application;

namespace WebApp.Services.Application;

public sealed class PortalEventLogService : IPortalEventLogService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<PortalEventLogService> _logger;

    public PortalEventLogService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<PortalEventLogService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task RecordAsync(PortalEventLogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrWhiteSpace(entry.Module))
            throw new ArgumentException("Module is required.", nameof(entry));
        if (string.IsNullOrWhiteSpace(entry.Action))
            throw new ArgumentException("Action is required.", nameof(entry));
        if (string.IsNullOrWhiteSpace(entry.Message))
            throw new ArgumentException("Message is required.", nameof(entry));

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            db.PortalEventLogs!.Add(new PortalEventLogRecord
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = entry.OccurredAtUtc?.ToUniversalTime() ?? DateTime.UtcNow,
                Module = Trim(entry.Module, 128) ?? "Unknown",
                Action = Trim(entry.Action, 128) ?? "Unknown",
                CompanyId = entry.CompanyId,
                CompanyName = Trim(entry.CompanyName, 256),
                JeevesCompanyCode = entry.JeevesCompanyCode,
                UserId = Trim(entry.UserId, 450),
                UserEmail = Trim(entry.UserEmail, 256),
                RequestPath = Trim(entry.RequestPath, 512),
                CorrelationId = Trim(entry.CorrelationId, 128),
                Severity = Trim(entry.Severity, 32) ?? "Error",
                Message = Trim(entry.Message, 2000) ?? "Unknown error",
                Exception = Trim(entry.Exception?.ToString(), 4000),
                AdditionalData = Trim(entry.AdditionalData, 2000)
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist portal event log for module {Module} action {Action}.",
                entry.Module,
                entry.Action);
        }
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

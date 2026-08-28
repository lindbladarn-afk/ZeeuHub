using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApp.Data;
using WebApp.Models.NotifyMe;
using WebApp.Services.Application;

namespace WebApp.Services.NotifyMe;

public sealed class NotifyMeAutomationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<NotifyMeAutomationOptions> _options;
    private readonly ILogger<NotifyMeAutomationWorker> _logger;

    public NotifyMeAutomationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<NotifyMeAutomationOptions> options,
        ILogger<NotifyMeAutomationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = _options.Value;
        await ProcessDueNotificationsAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, settings.PollIntervalMinutes)));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessDueNotificationsAsync(stoppingToken);
        }
    }

    private async Task ProcessDueNotificationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            var applicationConnectionContextService = scope.ServiceProvider.GetRequiredService<IApplicationConnectionContextService>();
            var connectionStringResolver = scope.ServiceProvider.GetRequiredService<IConnectionStringResolver>();
            var executionService = scope.ServiceProvider.GetRequiredService<INotifyMeExecutionService>();

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var portalConnection = (SqlConnection)dbContext.Database.GetDbConnection();
            if (portalConnection.State != System.Data.ConnectionState.Open)
                await dbContext.Database.OpenConnectionAsync(cancellationToken);

            var dueNotifications = (await portalConnection.QueryAsync<DueNotificationRow>(new CommandDefinition(@"
WITH CompanyCodeMap AS (
    SELECT
        c.Id AS [CompanyId],
        c.Name AS [CompanyName],
        c.DefaultJeevesCompanyCode AS [CompanyCode],
        CAST(1 AS bit) AS [IsDefault],
        CAST(0 AS int) AS [SortOrder]
    FROM [Identity].[Companies] c
    WHERE c.DefaultJeevesCompanyCode IS NOT NULL

    UNION ALL

    SELECT
        cjc.CompanyId AS [CompanyId],
        c.Name AS [CompanyName],
        cjc.CompanyCode AS [CompanyCode],
        cjc.IsDefault AS [IsDefault],
        cjc.SortOrder AS [SortOrder]
    FROM [Identity].[CompanyJeevesCompanies] cjc
    INNER JOIN [Identity].[Companies] c
        ON c.Id = cjc.CompanyId
    WHERE cjc.IsActive = 1
),
ResolvedCompanyCodeMap AS (
    SELECT
        CompanyId,
        CompanyName,
        CompanyCode,
        ROW_NUMBER() OVER (
            PARTITION BY CompanyCode
            ORDER BY IsDefault DESC, SortOrder ASC, CompanyName ASC
        ) AS [RowNo]
    FROM CompanyCodeMap
)
SELECT TOP (@BatchSize)
    c.CompanyId AS [CompanyId],
    c.CompanyName AS [CompanyName],
    n.foretagkod AS [CompanyCode],
    n.q_zu_notcenter_nr AS [NotificationId]
FROM dbo.q_zu_notcenter n
INNER JOIN ResolvedCompanyCodeMap c
    ON c.CompanyCode = n.foretagkod
   AND c.RowNo = 1
WHERE ISNULL(n.q_zu_notcenter_in_use, '0') = '1'
  AND n.q_zu_notcenter_execdat IS NOT NULL
  AND n.q_zu_notcenter_execdat <= SYSUTCDATETIME()
ORDER BY n.q_zu_notcenter_execdat ASC, n.q_zu_notcenter_nr ASC",
                new { BatchSize = Math.Max(1, _options.Value.BatchSize) },
                cancellationToken: cancellationToken))).AsList();

            if (dueNotifications.Count == 0)
            {
                _logger.LogDebug("NotifyMe worker found no due notifications.");
                return;
            }

            _logger.LogInformation("NotifyMe worker found {Count} due notifications.", dueNotifications.Count);

            var resolvedConnections = new Dictionary<Guid, string>();

            foreach (var notification in dueNotifications)
            {
                try
                {
                    if (!resolvedConnections.TryGetValue(notification.CompanyId, out var connectionString))
                    {
                        var connectionMappings = await applicationConnectionContextService.GetConnectionStringsAsync(dbContext, notification.CompanyId);
                        var activeMapping = connectionMappings.FirstOrDefault(x => x.IsActive) ?? connectionMappings.FirstOrDefault();
                        if (activeMapping == null)
                        {
                            _logger.LogWarning("NotifyMe worker could not resolve active connection mapping for company {CompanyId}", notification.CompanyId);
                            continue;
                        }

                        var resolved = await connectionStringResolver.ResolveAsync(connectionMappings, activeMapping.Id, notification.CompanyId);
                        if (!resolved.Success || string.IsNullOrWhiteSpace(resolved.Value))
                        {
                            _logger.LogWarning("NotifyMe worker could not resolve connection string for company {CompanyId}: {Error}", notification.CompanyId, resolved.Error);
                            continue;
                        }

                        connectionString = resolved.Value;
                        resolvedConnections[notification.CompanyId] = connectionString;
                    }

                    await executionService.RunScheduledNotificationAsync(
                        connectionString,
                        notification.CompanyCode,
                        notification.NotificationId,
                        cancellationToken);

                    _logger.LogInformation(
                        "NotifyMe worker executed notification {NotificationId} for company {CompanyId}/{CompanyCode}.",
                        notification.NotificationId,
                        notification.CompanyId,
                        notification.CompanyCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "NotifyMe worker failed for company {CompanyId}/{CompanyCode}, notification {NotificationId}",
                        notification.CompanyId,
                        notification.CompanyCode,
                        notification.NotificationId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotifyMe worker failed while processing due notifications.");
        }
    }

    private sealed class DueNotificationRow
    {
        public Guid CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public int CompanyCode { get; set; }
        public int NotificationId { get; set; }
    }
}

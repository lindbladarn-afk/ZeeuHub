using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using WebApp.Data;
using WebApp.Models.ActionCenter;
using WebApp.Models.BackgroundJobs;
using WebApp.Models.Integration;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Application.Infrastructure;

// Removes short-lived portal data that should not accumulate indefinitely.
public sealed class DataRetentionService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IOptions<DataRetentionOptions> _options;
    private readonly ILogger<DataRetentionService> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly IExcelImportBackgroundFileStore _excelImportFileStore;

    public DataRetentionService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IOptions<DataRetentionOptions> options,
        ILogger<DataRetentionService> logger,
        IWebHostEnvironment environment,
        IExcelImportBackgroundFileStore excelImportFileStore)
    {
        _dbContextFactory = dbContextFactory;
        _options = options;
        _logger = logger;
        _environment = environment;
        _excelImportFileStore = excelImportFileStore;
    }

    public async Task<DataRetentionReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var settings = _options.Value;
        if (!settings.Enabled)
        {
            _logger.LogDebug("Data retention is disabled.");
            return DataRetentionReport.Disabled;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var utcNow = DateTime.UtcNow;
        var report = new DataRetentionReport();
        report.BankReconciliationFilesDeleted = CleanupBankReconciliationUploads(utcNow, settings.BankReconciliationUploadRetentionDays, cancellationToken);
        report.ExcelImportBackgroundFilesDeleted = await CleanupExcelImportBackgroundFilesAsync(
            db,
            utcNow,
            settings.ExcelImportBackgroundFilesRetentionDays,
            cancellationToken);

        report.BackgroundJobsDeleted = await DeleteBackgroundJobsAsync(db, utcNow, settings.BackgroundJobsRetentionDays, cancellationToken);
        report.BackgroundJobRuntimeEventsDeleted = await DeleteBackgroundJobRuntimeEventsAsync(db, utcNow, settings.BackgroundJobRuntimeEventsRetentionDays, cancellationToken);
        report.PortalEventLogsDeleted = await DeletePortalEventLogsAsync(db, utcNow, settings.PortalEventLogsRetentionDays, cancellationToken);
        report.AiQueryLogsDeleted = await DeleteAiQueryLogsAsync(db, utcNow, settings.AiQueryLogsRetentionDays, cancellationToken);
        report.ExcelImportLogsDeleted = await DeleteExcelImportLogsAsync(db, utcNow, settings.ExcelImportLogsRetentionDays, cancellationToken);
        report.FlowEngineJobsDeleted = await DeleteFlowEngineJobsAsync(db, utcNow, settings.FlowEngineJobsRetentionDays, cancellationToken);
        report.ActionCenterItemStatesDeleted = await DeleteActionCenterItemStatesAsync(db, utcNow, settings.ActionCenterItemStatesRetentionDays, cancellationToken);
        report.AuthenticationTicketsDeleted = settings.PurgeExpiredAuthenticationTickets
            ? await DeleteExpiredAuthenticationTicketsAsync(db, utcNow, cancellationToken)
            : 0;

        if (report.TotalDeleted == 0)
        {
            _logger.LogDebug("Data retention found no expired items.");
            return report;
        }

        _logger.LogInformation(
            "Data retention removed {TotalDeleted} items: CAMT files {BankReconciliationFilesDeleted}, Excel import files {ExcelImportBackgroundFilesDeleted}, background jobs {BackgroundJobsDeleted}, runtime events {BackgroundJobRuntimeEventsDeleted}, event logs {PortalEventLogsDeleted}, AI queries {AiQueryLogsDeleted}, Excel imports {ExcelImportLogsDeleted}, FlowEngine jobs {FlowEngineJobsDeleted}, action center items {ActionCenterItemStatesDeleted}, auth tickets {AuthenticationTicketsDeleted}.",
            report.TotalDeleted,
            report.BankReconciliationFilesDeleted,
            report.ExcelImportBackgroundFilesDeleted,
            report.BackgroundJobsDeleted,
            report.BackgroundJobRuntimeEventsDeleted,
            report.PortalEventLogsDeleted,
            report.AiQueryLogsDeleted,
            report.ExcelImportLogsDeleted,
            report.FlowEngineJobsDeleted,
            report.ActionCenterItemStatesDeleted,
            report.AuthenticationTicketsDeleted);

        return report;
    }

    private async Task<int> CleanupExcelImportBackgroundFilesAsync(
        ApplicationDbContext db,
        DateTime utcNow,
        int retentionDays,
        CancellationToken cancellationToken)
    {
        var cutoff = GetCutoffUtc(utcNow, retentionDays);
        if (!cutoff.HasValue)
            return 0;

        var terminalStatuses = new[]
        {
            BackgroundJobStatus.Completed.ToString(),
            BackgroundJobStatus.Failed.ToString(),
            BackgroundJobStatus.Canceled.ToString()
        };
        var payloads = await db.BackgroundJobs!
            .AsNoTracking()
            .Where(job =>
                job.JobType == ExcelImportBackgroundJobConstants.ExecuteJobType &&
                !terminalStatuses.Contains(job.Status))
            .Select(job => job.PayloadJson)
            .ToListAsync(cancellationToken);
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var protectedPaths = new HashSet<string>(comparer);
        foreach (var payloadJson in payloads)
        {
            try
            {
                var path = ExcelImportBackgroundJobPayload.FromJson(payloadJson).FilePath;
                if (!string.IsNullOrWhiteSpace(path))
                    protectedPaths.Add(Path.GetFullPath(path));
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or ArgumentException or NotSupportedException)
            {
                _logger.LogWarning(ex, "Could not read an active Excel import job payload during retention cleanup.");
            }
        }

        return _excelImportFileStore.CleanupExpired(cutoff.Value, protectedPaths, cancellationToken);
    }

    private int CleanupBankReconciliationUploads(DateTime utcNow, int retentionDays, CancellationToken cancellationToken)
    {
        var cutoff = GetCutoffUtc(utcNow, retentionDays);
        if (cutoff is null)
            return 0;

        var root = Path.Combine(_environment.ContentRootPath, "App_Data", "Integration", "BankReconciliation", "camt053", "session");
        if (!Directory.Exists(root))
            return 0;

        var deleted = 0;
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(path);
                if (info.LinkTarget is not null || info.LastWriteTimeUtc >= cutoff.Value)
                    continue;

                info.Delete();
                deleted++;
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not remove an expired CAMT upload file.");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Could not remove an expired CAMT upload file due to access restrictions.");
            }
        }

        return deleted;
    }

    private static async Task<int> DeleteBackgroundJobsAsync(ApplicationDbContext db, DateTime utcNow, int retentionDays, CancellationToken cancellationToken)
    {
        var cutoff = GetCutoffUtc(utcNow, retentionDays);
        if (cutoff is null)
            return 0;

        var terminalStatuses = new[]
        {
            BackgroundJobStatus.Completed.ToString(),
            BackgroundJobStatus.Failed.ToString(),
            BackgroundJobStatus.Canceled.ToString()
        };

        var ids = await db.BackgroundJobs!
            .AsNoTracking()
            .Where(job =>
                job.CompletedAtUtc.HasValue &&
                job.CompletedAtUtc.Value < cutoff.Value &&
                terminalStatuses.Contains(job.Status))
            .Select(job => job.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        db.BackgroundJobs!.RemoveRange(ids.Select(id => new BackgroundJobRecord { Id = id }));
        await db.SaveChangesAsync(cancellationToken);
        return ids.Count;
    }

    private static async Task<int> DeleteBackgroundJobRuntimeEventsAsync(ApplicationDbContext db, DateTime utcNow, int retentionDays, CancellationToken cancellationToken)
    {
        var cutoff = GetCutoffUtc(utcNow, retentionDays);
        if (cutoff is null)
            return 0;

        var ids = await db.BackgroundJobRuntimeEvents!
            .AsNoTracking()
            .Where(item => item.OccurredAtUtc < cutoff.Value)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        db.BackgroundJobRuntimeEvents!.RemoveRange(ids.Select(id => new BackgroundJobRuntimeEventRecord { Id = id }));
        await db.SaveChangesAsync(cancellationToken);
        return ids.Count;
    }

    private static async Task<int> DeletePortalEventLogsAsync(ApplicationDbContext db, DateTime utcNow, int retentionDays, CancellationToken cancellationToken)
    {
        var cutoff = GetCutoffUtc(utcNow, retentionDays);
        if (cutoff is null)
            return 0;

        var ids = await db.PortalEventLogs!
            .AsNoTracking()
            .Where(item => item.OccurredAtUtc < cutoff.Value)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        db.PortalEventLogs!.RemoveRange(ids.Select(id => new WebApp.Models.Application.PortalEventLogRecord { Id = id }));
        await db.SaveChangesAsync(cancellationToken);
        return ids.Count;
    }

    private static async Task<int> DeleteAiQueryLogsAsync(ApplicationDbContext db, DateTime utcNow, int retentionDays, CancellationToken cancellationToken)
    {
        var cutoff = GetCutoffUtc(utcNow, retentionDays);
        if (cutoff is null)
            return 0;

        var ids = await db.AiQueryLogs!
            .AsNoTracking()
            .Where(item => item.CreatedAtUtc < cutoff.Value)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        db.AiQueryLogs!.RemoveRange(ids.Select(id => new WebApp.Models.Telemetry.AiQueryLog { Id = id }));
        await db.SaveChangesAsync(cancellationToken);
        return ids.Count;
    }

    private static async Task<int> DeleteExcelImportLogsAsync(ApplicationDbContext db, DateTime utcNow, int retentionDays, CancellationToken cancellationToken)
    {
        var cutoff = GetCutoffUtc(utcNow, retentionDays);
        if (cutoff is null)
            return 0;

        var ids = await db.ExcelImportLogs!
            .AsNoTracking()
            .Where(item => item.CreatedAtUtc < cutoff.Value)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        db.ExcelImportLogs!.RemoveRange(ids.Select(id => new WebApp.Models.Telemetry.ExcelImportLog { Id = id }));
        await db.SaveChangesAsync(cancellationToken);
        return ids.Count;
    }

    private static async Task<int> DeleteFlowEngineJobsAsync(ApplicationDbContext db, DateTime utcNow, int retentionDays, CancellationToken cancellationToken)
    {
        var cutoff = GetCutoffUtc(utcNow, retentionDays);
        if (cutoff is null)
            return 0;

        var terminalStatuses = new[]
        {
            FlowEngineJobStatus.Succeeded.ToString(),
            FlowEngineJobStatus.Failed.ToString(),
            FlowEngineJobStatus.Cancelled.ToString()
        };

        var ids = await db.FlowEngineJobs!
            .AsNoTracking()
            .Where(item =>
                item.FinishedAtUtc.HasValue &&
                item.FinishedAtUtc.Value < cutoff.Value &&
                terminalStatuses.Contains(item.Status))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        db.FlowEngineJobs!.RemoveRange(ids.Select(id => new FlowEngineJobRecord { Id = id }));
        await db.SaveChangesAsync(cancellationToken);
        return ids.Count;
    }

    private static async Task<int> DeleteActionCenterItemStatesAsync(ApplicationDbContext db, DateTime utcNow, int retentionDays, CancellationToken cancellationToken)
    {
        var cutoff = GetCutoffUtc(utcNow, retentionDays);
        if (cutoff is null)
            return 0;

        var terminalStatuses = new[]
        {
            ActionCenterItemStatus.Completed,
            ActionCenterItemStatus.Dismissed
        };

        var ids = await db.ActionCenterItemStates!
            .AsNoTracking()
            .Where(item =>
                item.CompletedAtUtc.HasValue &&
                item.CompletedAtUtc.Value < cutoff.Value &&
                terminalStatuses.Contains(item.Status))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        db.ActionCenterItemStates!.RemoveRange(ids.Select(id => new ActionCenterItemState { Id = id }));
        await db.SaveChangesAsync(cancellationToken);
        return ids.Count;
    }

    private static async Task<int> DeleteExpiredAuthenticationTicketsAsync(ApplicationDbContext db, DateTime utcNow, CancellationToken cancellationToken)
    {
        var ids = await db.PortalAuthenticationTickets!
            .AsNoTracking()
            .Where(item => item.ExpiresAtUtc < utcNow)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
            return 0;

        db.PortalAuthenticationTickets!.RemoveRange(ids.Select(id => new WebApp.Models.Application.PortalAuthenticationTicketRecord { Id = id }));
        await db.SaveChangesAsync(cancellationToken);
        return ids.Count;
    }

    private static DateTime? GetCutoffUtc(DateTime utcNow, int retentionDays)
        => retentionDays <= 0 ? null : utcNow.AddDays(-retentionDays);
}

public sealed record DataRetentionReport
{
    public static DataRetentionReport Disabled { get; } = new() { IsEnabled = false };

    public bool IsEnabled { get; init; } = true;
    public int BackgroundJobsDeleted { get; set; }
    public int BackgroundJobRuntimeEventsDeleted { get; set; }
    public int PortalEventLogsDeleted { get; set; }
    public int AiQueryLogsDeleted { get; set; }
    public int ExcelImportLogsDeleted { get; set; }
    public int FlowEngineJobsDeleted { get; set; }
    public int ActionCenterItemStatesDeleted { get; set; }
    public int AuthenticationTicketsDeleted { get; set; }
    public int BankReconciliationFilesDeleted { get; set; }
    public int ExcelImportBackgroundFilesDeleted { get; set; }

    public int TotalDeleted =>
        BankReconciliationFilesDeleted +
        ExcelImportBackgroundFilesDeleted +
        BackgroundJobsDeleted +
        BackgroundJobRuntimeEventsDeleted +
        PortalEventLogsDeleted +
        AiQueryLogsDeleted +
        ExcelImportLogsDeleted +
        FlowEngineJobsDeleted +
        ActionCenterItemStatesDeleted +
        AuthenticationTicketsDeleted;
}

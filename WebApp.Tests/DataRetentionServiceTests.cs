using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebApp.Data;
using WebApp.Models.ActionCenter;
using WebApp.Models.BackgroundJobs;
using WebApp.Models.Application;
using WebApp.Models.Integration;
using WebApp.Models.Telemetry;
using WebApp.Services.Application.Infrastructure;
using WebApp.Services.Application;
using WebApp.Services.Telemetry;
using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

public sealed class DataRetentionServiceTests
{
    [Fact]
    public async Task RunAsync_Removes_Expired_Operational_Data_And_Keeps_Active_Records()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var dbFactory = new TestDbContextFactory(dbName);
        var contentRoot = Path.Combine(Path.GetTempPath(), $"retention-{Guid.NewGuid():N}");
        var uploadRoot = Path.Combine(contentRoot, "App_Data", "Integration", "BankReconciliation", "camt053", "session", "session-1");
        Directory.CreateDirectory(uploadRoot);
        var oldUpload = Path.Combine(uploadRoot, "old.xml");
        var freshUpload = Path.Combine(uploadRoot, "fresh.xml");
        await File.WriteAllTextAsync(oldUpload, "old");
        await File.WriteAllTextAsync(freshUpload, "fresh");
        File.SetLastWriteTimeUtc(oldUpload, DateTime.UtcNow.AddDays(-20));
        File.SetLastWriteTimeUtc(freshUpload, DateTime.UtcNow.AddDays(-2));
        var excelFileRoot = Path.Combine(contentRoot, "excel-import-jobs");
        Directory.CreateDirectory(excelFileRoot);
        var oldExcelFile = Path.Combine(excelFileRoot, "old.xlsx");
        var freshExcelFile = Path.Combine(excelFileRoot, "fresh.xlsx");
        var activeExcelFile = Path.Combine(excelFileRoot, "active.xlsx");
        await File.WriteAllTextAsync(oldExcelFile, "old");
        await File.WriteAllTextAsync(freshExcelFile, "fresh");
        await File.WriteAllTextAsync(activeExcelFile, "active");
        File.SetLastWriteTimeUtc(oldExcelFile, DateTime.UtcNow.AddDays(-5));
        File.SetLastWriteTimeUtc(freshExcelFile, DateTime.UtcNow);
        File.SetLastWriteTimeUtc(activeExcelFile, DateTime.UtcNow.AddDays(-5));
        await SeedAsync(dbFactory);
        await AddActiveExcelImportJobAsync(dbFactory, activeExcelFile);

        var environment = new TestHostEnvironment { ContentRootPath = contentRoot };
        var excelFileStore = new LocalExcelImportBackgroundFileStore(
            environment,
            Options.Create(new ExcelImportBackgroundFileStoreOptions
            {
                StorageRoot = excelFileRoot
            }),
            NullLogger<LocalExcelImportBackgroundFileStore>.Instance);

        var service = new DataRetentionService(
            dbFactory,
            Options.Create(new DataRetentionOptions
            {
                BackgroundJobsRetentionDays = 10,
                BackgroundJobRuntimeEventsRetentionDays = 10,
                PortalEventLogsRetentionDays = 10,
                AiQueryLogsRetentionDays = 10,
                ExcelImportLogsRetentionDays = 10,
                ExcelImportBackgroundFilesRetentionDays = 2,
                FlowEngineJobsRetentionDays = 10,
                ActionCenterItemStatesRetentionDays = 10,
                BankReconciliationUploadRetentionDays = 10
            }),
            NullLogger<DataRetentionService>.Instance,
            environment,
            excelFileStore);

        var report = await service.RunAsync();

        Assert.Equal(1, report.BackgroundJobsDeleted);
        Assert.Equal(1, report.BackgroundJobRuntimeEventsDeleted);
        Assert.Equal(1, report.PortalEventLogsDeleted);
        Assert.Equal(1, report.AiQueryLogsDeleted);
        Assert.Equal(1, report.ExcelImportLogsDeleted);
        Assert.Equal(1, report.FlowEngineJobsDeleted);
        Assert.Equal(1, report.ActionCenterItemStatesDeleted);
        Assert.Equal(1, report.AuthenticationTicketsDeleted);
        Assert.Equal(1, report.BankReconciliationFilesDeleted);
        Assert.Equal(1, report.ExcelImportBackgroundFilesDeleted);
        Assert.False(File.Exists(oldUpload));
        Assert.True(File.Exists(freshUpload));
        Assert.False(File.Exists(oldExcelFile));
        Assert.True(File.Exists(freshExcelFile));
        Assert.True(File.Exists(activeExcelFile));

        await using var db = await dbFactory.CreateDbContextAsync();

        Assert.Equal(3, await db.BackgroundJobs!.CountAsync());
        Assert.Equal(1, await db.BackgroundJobs!.CountAsync(job => job.Status == BackgroundJobStatus.Running.ToString()));
        Assert.Equal(1, await db.BackgroundJobs!.CountAsync(job => job.Status == BackgroundJobStatus.Queued.ToString()));
        Assert.Equal(1, await db.BackgroundJobs!.CountAsync(job => job.Status == BackgroundJobStatus.Completed.ToString()));

        Assert.Single(await db.BackgroundJobRuntimeEvents!.ToListAsync());
        Assert.Single(await db.PortalEventLogs!.ToListAsync());
        Assert.Single(await db.AiQueryLogs!.ToListAsync());
        Assert.Single(await db.ExcelImportLogs!.ToListAsync());
        Assert.Single(await db.FlowEngineJobs!.ToListAsync());
        Assert.Single(await db.ActionCenterItemStates!.ToListAsync());
        Assert.Single(await db.PortalAuthenticationTickets!.ToListAsync());
        Directory.Delete(contentRoot, recursive: true);
    }

    [Fact]
    public async Task LogServices_Trim_Long_Text_When_Persisting()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var dbFactory = new TestDbContextFactory(dbName);

        var aiService = new TelemetryAiQueryService(dbFactory);
        await aiService.LogAiQueryAsync(
            companyId: Guid.NewGuid(),
            userId: Guid.NewGuid().ToString("N"),
            question: new string('q', 5000),
            allowed: true,
            wasSuccessful: true,
            sqlText: new string('s', 9000),
            errorMessage: new string('e', 5000),
            promptTokens: 1,
            completionTokens: 2,
            totalTokens: 3,
            details: new AiQueryTelemetryDetails
            {
                ResponseId = Guid.NewGuid(),
                PromptVersion = new string('p', 200),
                ModelDeployment = new string('m', 300),
                ErrorCode = "query_failed",
                VerificationStatus = "verified",
                DurationMs = 1200,
                PlanningDurationMs = 400,
                SqlDurationMs = 500,
                SummaryDurationMs = 300,
                ModelRetryCount = 1,
                RowCount = 25,
                WasTruncated = false
            });

        var eventService = new PortalEventLogService(dbFactory, NullLogger<PortalEventLogService>.Instance);
        await eventService.RecordAsync(new PortalEventLogEntry
        {
            Module = "AI",
            Action = "Query",
            Message = new string('m', 5000),
            Exception = new InvalidOperationException(new string('x', 10000)),
            AdditionalData = new string('a', 6000)
        });

        await using var db = await dbFactory.CreateDbContextAsync();

        var aiLog = await db.AiQueryLogs!.SingleAsync();
        Assert.Equal(2000, aiLog.Question!.Length);
        Assert.Equal(4000, aiLog.SqlText!.Length);
        Assert.Equal(2000, aiLog.ErrorMessage!.Length);
        Assert.NotNull(aiLog.ResponseId);
        Assert.Equal(100, aiLog.PromptVersion!.Length);
        Assert.Equal(200, aiLog.ModelDeployment!.Length);
        Assert.Equal("verified", aiLog.VerificationStatus);
        Assert.Equal(1200, aiLog.DurationMs);
        Assert.Equal(25, aiLog.RowCount);

        var portalEventLog = await db.PortalEventLogs!.SingleAsync();
        Assert.Equal(2000, portalEventLog.Message.Length);
        Assert.Equal(4000, portalEventLog.Exception!.Length);
        Assert.Equal(2000, portalEventLog.AdditionalData!.Length);
    }

    private static async Task SeedAsync(TestDbContextFactory dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var old = DateTime.UtcNow.AddDays(-20);
        var fresh = DateTime.UtcNow.AddDays(-2);
        var future = DateTime.UtcNow.AddDays(2);

        db.BackgroundJobs!.AddRange(
            new BackgroundJobRecord
            {
                Id = Guid.NewGuid(),
                CompanyId = Guid.NewGuid(),
                JobType = "Import",
                Status = BackgroundJobStatus.Completed.ToString(),
                CreatedAtUtc = old,
                UpdatedAtUtc = old,
                QueuedAtUtc = old,
                AvailableAtUtc = old,
                CompletedAtUtc = old
            },
            new BackgroundJobRecord
            {
                Id = Guid.NewGuid(),
                CompanyId = Guid.NewGuid(),
                JobType = "Import",
                Status = BackgroundJobStatus.Completed.ToString(),
                CreatedAtUtc = fresh,
                UpdatedAtUtc = fresh,
                QueuedAtUtc = fresh,
                AvailableAtUtc = fresh,
                CompletedAtUtc = fresh
            },
            new BackgroundJobRecord
            {
                Id = Guid.NewGuid(),
                CompanyId = Guid.NewGuid(),
                JobType = "Import",
                Status = BackgroundJobStatus.Running.ToString(),
                CreatedAtUtc = fresh,
                UpdatedAtUtc = fresh,
                QueuedAtUtc = fresh,
                AvailableAtUtc = fresh,
                StartedAtUtc = fresh,
                LastHeartbeatAtUtc = fresh,
                LeaseExpiresAtUtc = future
            });

        db.BackgroundJobRuntimeEvents!.AddRange(
            new BackgroundJobRuntimeEventRecord
            {
                Id = Guid.NewGuid(),
                CompanyId = Guid.NewGuid(),
                EventType = "completed",
                Source = "test",
                Title = "Old",
                StatusLabel = "Completed",
                StatusTone = "success",
                Summary = "old",
                OccurredAtUtc = old
            },
            new BackgroundJobRuntimeEventRecord
            {
                Id = Guid.NewGuid(),
                CompanyId = Guid.NewGuid(),
                EventType = "running",
                Source = "test",
                Title = "Fresh",
                StatusLabel = "Running",
                StatusTone = "info",
                Summary = "fresh",
                OccurredAtUtc = fresh
            });

        db.PortalEventLogs!.AddRange(
            new PortalEventLogRecord
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = old,
                Module = "Module",
                Action = "Action",
                Severity = "Error",
                Message = "old"
            },
            new PortalEventLogRecord
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = fresh,
                Module = "Module",
                Action = "Action",
                Severity = "Error",
                Message = "fresh"
            });

        db.AiQueryLogs!.AddRange(
            new AiQueryLog
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = old,
                WasAllowed = true,
                WasSuccessful = true,
                Question = "old"
            },
            new AiQueryLog
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = fresh,
                WasAllowed = true,
                WasSuccessful = true,
                Question = "fresh"
            });

        db.ExcelImportLogs!.AddRange(
            new ExcelImportLog
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = old,
                TotalRows = 10,
                ValidRows = 9,
                InvalidRows = 1
            },
            new ExcelImportLog
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = fresh,
                TotalRows = 10,
                ValidRows = 10,
                InvalidRows = 0
            });

        db.FlowEngineJobs!.AddRange(
            new FlowEngineJobRecord
            {
                Id = Guid.NewGuid(),
                CompanyId = Guid.NewGuid(),
                Status = FlowEngineJobStatus.Succeeded.ToString(),
                CreatedAtUtc = old,
                FinishedAtUtc = old
            },
            new FlowEngineJobRecord
            {
                Id = Guid.NewGuid(),
                CompanyId = Guid.NewGuid(),
                Status = FlowEngineJobStatus.Running.ToString(),
                CreatedAtUtc = fresh,
                StartedAtUtc = fresh
            });

        db.ActionCenterItemStates!.AddRange(
            new ActionCenterItemState
            {
                ExternalId = "old",
                Status = ActionCenterItemStatus.Completed,
                CompanyId = Guid.NewGuid(),
                UserId = Guid.NewGuid().ToString("N"),
                UpdatedAtUtc = old,
                CompletedAtUtc = old
            },
            new ActionCenterItemState
            {
                ExternalId = "fresh",
                Status = ActionCenterItemStatus.Active,
                CompanyId = Guid.NewGuid(),
                UserId = Guid.NewGuid().ToString("N"),
                UpdatedAtUtc = fresh
            });

        db.PortalAuthenticationTickets!.AddRange(
            new PortalAuthenticationTicketRecord
            {
                Id = "expired",
                UserId = Guid.NewGuid().ToString("N"),
                Payload = [1, 2, 3],
                CreatedAtUtc = old,
                UpdatedAtUtc = old,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
            },
            new PortalAuthenticationTicketRecord
            {
                Id = "valid",
                UserId = Guid.NewGuid().ToString("N"),
                Payload = [1, 2, 3],
                CreatedAtUtc = fresh,
                UpdatedAtUtc = fresh,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1)
            });

        await db.SaveChangesAsync();
    }

    private static async Task AddActiveExcelImportJobAsync(
        TestDbContextFactory dbFactory,
        string filePath)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var companyId = Guid.NewGuid();
        db.BackgroundJobs!.Add(new BackgroundJobRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            JobType = ExcelImportBackgroundJobConstants.ExecuteJobType,
            Status = BackgroundJobStatus.Queued.ToString(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            QueuedAtUtc = now,
            AvailableAtUtc = now,
            PayloadJson = new ExcelImportBackgroundJobPayload
            {
                CompanyId = companyId,
                FilePath = filePath,
                ImportType = "budget",
                ImportedBy = "test-user"
            }.ToJson()
        });
        await db.SaveChangesAsync();
    }

    private sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public TestDbContextFactory(string dbName)
        {
            _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
        }

        public ApplicationDbContext CreateDbContext()
            => new(_options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}

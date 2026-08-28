using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WebApp.Data;
using WebApp.Models.BackgroundJobs;

namespace WebApp.Services.Application.BackgroundJobs;

public sealed class BackgroundJobDbStore : IBackgroundJobStore
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public BackgroundJobDbStore(
        IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public BackgroundJobSnapshot Enqueue(BackgroundJobEnqueueRequest request, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.CompanyId == Guid.Empty)
            throw new ArgumentException("CompanyId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.JobType))
            throw new ArgumentException("JobType is required.", nameof(request));

        using var db = _dbContextFactory.CreateDbContext();

        var entity = new BackgroundJobRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = request.CompanyId,
            CreatedByUserId = Normalize(request.CreatedByUserId, 450),
            CreatedByEmail = Normalize(request.CreatedByEmail, 256),
            JobType = Normalize(request.JobType, 128) ?? throw new ArgumentException("JobType is required.", nameof(request)),
            Status = BackgroundJobStatus.Queued.ToString(),
            CorrelationKey = Normalize(request.CorrelationKey, 128),
            PayloadJson = string.IsNullOrWhiteSpace(request.PayloadJson) ? "{}" : request.PayloadJson,
            AttemptCount = 0,
            MaxAttempts = request.MaxAttempts <= 0 ? 3 : request.MaxAttempts,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
            QueuedAtUtc = utcNow,
            AvailableAtUtc = request.AvailableAtUtc?.ToUniversalTime() ?? utcNow
        };

        db.BackgroundJobs!.Add(entity);
        db.SaveChanges();
        return Map(entity);
    }

    public BackgroundJobSnapshot? TryClaimNext(
        string workerId,
        DateTime utcNow,
        TimeSpan leaseDuration,
        Guid? companyId = null,
        IReadOnlyCollection<string>? allowedJobTypes = null)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            throw new ArgumentException("Worker id is required.", nameof(workerId));
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "Lease duration must be positive.");

        using var db = _dbContextFactory.CreateDbContext();
        var strategy = db.Database.CreateExecutionStrategy();

        return strategy.Execute(() =>
        {
            db.Database.OpenConnection();
            using var transaction = db.Database.BeginTransaction(IsolationLevel.ReadCommitted);
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();

            var sql = BuildClaimSql(companyId, allowedJobTypes);
            command.CommandText = sql;

            AddParameter(command, "@queuedStatus", BackgroundJobStatus.Queued.ToString());
            AddParameter(command, "@runningStatus", BackgroundJobStatus.Running.ToString());
            AddParameter(command, "@utcNow", utcNow);
            AddParameter(command, "@leaseExpiresAtUtc", utcNow.Add(leaseDuration));
            AddParameter(command, "@workerId", workerId);

            if (companyId.HasValue)
                AddParameter(command, "@companyId", companyId.Value);

            if (allowedJobTypes is { Count: > 0 })
            {
                var index = 0;
                foreach (var jobType in allowedJobTypes.Where(static value => !string.IsNullOrWhiteSpace(value)))
                {
                    AddParameter(command, $"@jobType{index++}", jobType);
                }
            }

            BackgroundJobSnapshot? snapshot;
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read())
                {
                    snapshot = null;
                }
                else
                {
                    snapshot = Map(reader);
                }
            }

            transaction.Commit();
            return snapshot;
        });
    }

    public IReadOnlyList<Guid> ListQueuedCompanyIds(
        DateTime utcNow,
        int take,
        IReadOnlyCollection<string>? allowedJobTypes = null)
    {
        var safeTake = take <= 0 ? 10 : take;

        using var db = _dbContextFactory.CreateDbContext();
        var query = db.BackgroundJobs!
            .AsNoTracking()
            .Where(item =>
                item.Status == BackgroundJobStatus.Queued.ToString() &&
                item.AvailableAtUtc <= utcNow);

        if (allowedJobTypes is { Count: > 0 })
        {
            var jobTypes = allowedJobTypes
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (jobTypes.Length > 0)
            {
                query = query.Where(item => jobTypes.Contains(item.JobType));
            }
        }

        return query
            .GroupBy(item => item.CompanyId)
            .Select(group => new
            {
                CompanyId = group.Key,
                NextAvailableAtUtc = group.Min(item => item.AvailableAtUtc),
                FirstCreatedAtUtc = group.Min(item => item.CreatedAtUtc)
            })
            .OrderBy(item => item.NextAvailableAtUtc)
            .ThenBy(item => item.FirstCreatedAtUtc)
            .Take(safeTake)
            .Select(item => item.CompanyId)
            .ToList();
    }

    public BackgroundJobSnapshot? Get(Guid companyId, Guid jobId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var entity = db.BackgroundJobs!
            .AsNoTracking()
            .FirstOrDefault(item => item.CompanyId == companyId && item.Id == jobId);

        return entity is null ? null : Map(entity);
    }

    public BackgroundJobSnapshot? FindActive(Guid companyId, string jobType, string correlationKey, Guid? excludeJobId = null)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("CompanyId is required.", nameof(companyId));
        if (string.IsNullOrWhiteSpace(jobType))
            throw new ArgumentException("JobType is required.", nameof(jobType));
        if (string.IsNullOrWhiteSpace(correlationKey))
            throw new ArgumentException("CorrelationKey is required.", nameof(correlationKey));

        var active = new[] { BackgroundJobStatus.Queued.ToString(), BackgroundJobStatus.Running.ToString() };

        using var db = _dbContextFactory.CreateDbContext();
        var query = db.BackgroundJobs!
            .AsNoTracking()
            .Where(item =>
                item.CompanyId == companyId
                && item.JobType == jobType
                && item.CorrelationKey == correlationKey
                && active.Contains(item.Status));

        if (excludeJobId.HasValue && excludeJobId.Value != Guid.Empty)
            query = query.Where(item => item.Id != excludeJobId.Value);

        var entity = query
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefault();

        return entity is null ? null : Map(entity);
    }

    public IReadOnlyList<BackgroundJobSnapshot> ListRecent(Guid companyId, int take)
    {
        var safeTake = take <= 0 ? 10 : take;

        using var db = _dbContextFactory.CreateDbContext();
        return db.BackgroundJobs!
            .AsNoTracking()
            .Where(item => item.CompanyId == companyId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(safeTake)
            .Select(MapExpression())
            .ToList();
    }

    public IReadOnlyList<BackgroundJobSnapshot> ListActive(Guid companyId, int take)
    {
        var safeTake = take <= 0 ? 10 : take;
        var active = new[] { BackgroundJobStatus.Queued.ToString(), BackgroundJobStatus.Running.ToString() };

        using var db = _dbContextFactory.CreateDbContext();
        return db.BackgroundJobs!
            .AsNoTracking()
            .Where(item => item.CompanyId == companyId && active.Contains(item.Status))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(safeTake)
            .Select(MapExpression())
            .ToList();
    }

    public BackgroundJobSnapshot Heartbeat(Guid companyId, Guid jobId, string workerId, DateTime utcNow, TimeSpan leaseDuration)
        => UpdateOwnedRunningJob(companyId, jobId, workerId, entity =>
        {
            entity.LastHeartbeatAtUtc = utcNow;
            entity.LeaseExpiresAtUtc = utcNow.Add(leaseDuration);
            entity.UpdatedAtUtc = utcNow;
        });

    public BackgroundJobSnapshot Complete(Guid companyId, Guid jobId, string workerId, DateTime utcNow, string? resultJson = null)
        => UpdateOwnedRunningJob(companyId, jobId, workerId, entity =>
        {
            entity.Status = BackgroundJobStatus.Completed.ToString();
            entity.CompletedAtUtc = utcNow;
            entity.UpdatedAtUtc = utcNow;
            entity.LastHeartbeatAtUtc = utcNow;
            entity.LeaseExpiresAtUtc = null;
            entity.ClaimedAtUtc = null;
            entity.ClaimedBy = null;
            entity.ErrorCode = null;
            entity.ErrorMessage = null;
            entity.LastResultJson = NormalizeLarge(resultJson);
        });

    public BackgroundJobSnapshot Fail(Guid companyId, Guid jobId, string workerId, DateTime utcNow, string? errorCode, string? errorMessage, TimeSpan? retryDelay = null, string? resultJson = null)
        => UpdateOwnedRunningJob(companyId, jobId, workerId, entity =>
        {
            entity.UpdatedAtUtc = utcNow;
            entity.LastHeartbeatAtUtc = utcNow;
            entity.ErrorCode = Normalize(errorCode, 64);
            entity.ErrorMessage = Normalize(errorMessage, 4000);
            entity.LastResultJson = NormalizeLarge(resultJson);

            var hasRetriesLeft = entity.AttemptCount < entity.MaxAttempts;
            if (retryDelay.HasValue && retryDelay.Value > TimeSpan.Zero && hasRetriesLeft)
            {
                entity.Status = BackgroundJobStatus.Queued.ToString();
                entity.QueuedAtUtc = utcNow;
                entity.AvailableAtUtc = utcNow.Add(retryDelay.Value);
                entity.ClaimedAtUtc = null;
                entity.ClaimedBy = null;
                entity.LeaseExpiresAtUtc = null;
                entity.StartedAtUtc = null;
                entity.CompletedAtUtc = null;
            }
            else
            {
                entity.Status = BackgroundJobStatus.Failed.ToString();
                entity.CompletedAtUtc = utcNow;
                entity.ClaimedAtUtc = null;
                entity.ClaimedBy = null;
                entity.LeaseExpiresAtUtc = null;
            }
        });

    public BackgroundJobSnapshot Cancel(Guid companyId, Guid jobId, DateTime utcNow, string? errorMessage = null)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var entity = LoadJob(db, companyId, jobId);

        entity.Status = BackgroundJobStatus.Canceled.ToString();
        entity.CompletedAtUtc = utcNow;
        entity.UpdatedAtUtc = utcNow;
        entity.ErrorMessage = Normalize(errorMessage, 4000);
        entity.ClaimedAtUtc = null;
        entity.ClaimedBy = null;
        entity.LeaseExpiresAtUtc = null;

        db.SaveChanges();
        return Map(entity);
    }

    public int RequeueExpiredLeases(DateTime utcNow, TimeSpan retryDelay)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var expired = db.BackgroundJobs!
            .Where(item =>
                item.Status == BackgroundJobStatus.Running.ToString() &&
                item.LeaseExpiresAtUtc.HasValue &&
                item.LeaseExpiresAtUtc < utcNow)
            .ToList();

        if (expired.Count == 0)
            return 0;

        foreach (var entity in expired)
        {
            var hasRetriesLeft = entity.AttemptCount < entity.MaxAttempts;
            entity.UpdatedAtUtc = utcNow;
            entity.ClaimedAtUtc = null;
            entity.ClaimedBy = null;
            entity.LastHeartbeatAtUtc = utcNow;
            entity.LeaseExpiresAtUtc = null;
            entity.ErrorCode ??= "lease_expired";
            entity.ErrorMessage ??= "Job lease expired before completion.";

            if (hasRetriesLeft)
            {
                entity.Status = BackgroundJobStatus.Queued.ToString();
                entity.QueuedAtUtc = utcNow;
                entity.AvailableAtUtc = utcNow.Add(retryDelay > TimeSpan.Zero ? retryDelay : TimeSpan.FromSeconds(5));
                entity.StartedAtUtc = null;
                entity.CompletedAtUtc = null;
            }
            else
            {
                entity.Status = BackgroundJobStatus.Failed.ToString();
                entity.CompletedAtUtc = utcNow;
            }
        }

        db.SaveChanges();
        return expired.Count;
    }

    private BackgroundJobSnapshot UpdateOwnedRunningJob(Guid companyId, Guid jobId, string workerId, Action<BackgroundJobRecord> mutate)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var entity = LoadJob(db, companyId, jobId);

        if (!string.Equals(entity.Status, BackgroundJobStatus.Running.ToString(), StringComparison.Ordinal))
            throw new InvalidOperationException($"Background job '{jobId}' is not running.");
        if (!string.Equals(entity.ClaimedBy, workerId, StringComparison.Ordinal))
            throw new InvalidOperationException($"Background job '{jobId}' is not claimed by worker '{workerId}'.");

        mutate(entity);
        db.SaveChanges();
        return Map(entity);
    }

    private static BackgroundJobRecord LoadJob(ApplicationDbContext db, Guid companyId, Guid jobId)
        => db.BackgroundJobs!
            .FirstOrDefault(item => item.CompanyId == companyId && item.Id == jobId)
            ?? throw new InvalidOperationException($"Background job '{jobId}' could not be found.");

    private static string BuildClaimSql(Guid? companyId, IReadOnlyCollection<string>? allowedJobTypes)
    {
        var sql = """
                  ;WITH candidate AS
                  (
                      SELECT TOP (1) *
                      FROM [Identity].[BackgroundJobs] WITH (UPDLOCK, READPAST, ROWLOCK)
                      WHERE [Status] = @queuedStatus
                        AND [AvailableAtUtc] <= @utcNow
                  """;

        if (companyId.HasValue)
            sql += "\n  AND [CompanyId] = @companyId";

        if (allowedJobTypes is { Count: > 0 })
        {
            var placeholders = allowedJobTypes
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select((_, index) => $"@jobType{index}")
                .ToArray();

            if (placeholders.Length > 0)
                sql += $"\n  AND [JobType] IN ({string.Join(", ", placeholders)})";
        }

        sql += """

                      ORDER BY [AvailableAtUtc], [CreatedAtUtc]
                  )
                  UPDATE candidate
                  SET [Status] = @runningStatus,
                      [UpdatedAtUtc] = @utcNow,
                      [StartedAtUtc] = COALESCE([StartedAtUtc], @utcNow),
                      [LastAttemptAtUtc] = @utcNow,
                      [ClaimedBy] = @workerId,
                      [ClaimedAtUtc] = @utcNow,
                      [LastHeartbeatAtUtc] = @utcNow,
                      [LeaseExpiresAtUtc] = @leaseExpiresAtUtc,
                      [AttemptCount] = [AttemptCount] + 1
                  OUTPUT
                      inserted.[Id],
                      inserted.[CompanyId],
                      inserted.[CreatedByUserId],
                      inserted.[CreatedByEmail],
                      inserted.[JobType],
                      inserted.[Status],
                      inserted.[CorrelationKey],
                      inserted.[PayloadJson],
                      inserted.[AttemptCount],
                      inserted.[MaxAttempts],
                      inserted.[CreatedAtUtc],
                      inserted.[UpdatedAtUtc],
                      inserted.[QueuedAtUtc],
                      inserted.[AvailableAtUtc],
                      inserted.[StartedAtUtc],
                      inserted.[LastAttemptAtUtc],
                      inserted.[ClaimedBy],
                      inserted.[ClaimedAtUtc],
                      inserted.[LastHeartbeatAtUtc],
                      inserted.[LeaseExpiresAtUtc],
                      inserted.[CompletedAtUtc],
                      inserted.[ErrorCode],
                      inserted.[ErrorMessage],
                      inserted.[LastResultJson];
                  """;

        return sql;
    }

    private static System.Linq.Expressions.Expression<Func<BackgroundJobRecord, BackgroundJobSnapshot>> MapExpression()
        => entity => new BackgroundJobSnapshot
        {
            Id = entity.Id,
            CompanyId = entity.CompanyId,
            CreatedByUserId = entity.CreatedByUserId,
            CreatedByEmail = entity.CreatedByEmail,
            JobType = entity.JobType,
            Status = entity.Status == nameof(BackgroundJobStatus.Running)
                ? BackgroundJobStatus.Running
                : entity.Status == nameof(BackgroundJobStatus.Completed)
                    ? BackgroundJobStatus.Completed
                    : entity.Status == nameof(BackgroundJobStatus.Failed)
                        ? BackgroundJobStatus.Failed
                        : entity.Status == nameof(BackgroundJobStatus.Canceled)
                            ? BackgroundJobStatus.Canceled
                            : BackgroundJobStatus.Queued,
            CorrelationKey = entity.CorrelationKey,
            PayloadJson = entity.PayloadJson,
            AttemptCount = entity.AttemptCount,
            MaxAttempts = entity.MaxAttempts,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            QueuedAtUtc = entity.QueuedAtUtc,
            AvailableAtUtc = entity.AvailableAtUtc,
            StartedAtUtc = entity.StartedAtUtc,
            LastAttemptAtUtc = entity.LastAttemptAtUtc,
            ClaimedBy = entity.ClaimedBy,
            ClaimedAtUtc = entity.ClaimedAtUtc,
            LastHeartbeatAtUtc = entity.LastHeartbeatAtUtc,
            LeaseExpiresAtUtc = entity.LeaseExpiresAtUtc,
            CompletedAtUtc = entity.CompletedAtUtc,
            ErrorCode = entity.ErrorCode,
            ErrorMessage = entity.ErrorMessage,
            LastResultJson = entity.LastResultJson
        };

    private static BackgroundJobSnapshot Map(BackgroundJobRecord entity)
        => new()
        {
            Id = entity.Id,
            CompanyId = entity.CompanyId,
            CreatedByUserId = entity.CreatedByUserId,
            CreatedByEmail = entity.CreatedByEmail,
            JobType = entity.JobType,
            Status = ParseStatus(entity.Status),
            CorrelationKey = entity.CorrelationKey,
            PayloadJson = entity.PayloadJson,
            AttemptCount = entity.AttemptCount,
            MaxAttempts = entity.MaxAttempts,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            QueuedAtUtc = entity.QueuedAtUtc,
            AvailableAtUtc = entity.AvailableAtUtc,
            StartedAtUtc = entity.StartedAtUtc,
            LastAttemptAtUtc = entity.LastAttemptAtUtc,
            ClaimedBy = entity.ClaimedBy,
            ClaimedAtUtc = entity.ClaimedAtUtc,
            LastHeartbeatAtUtc = entity.LastHeartbeatAtUtc,
            LeaseExpiresAtUtc = entity.LeaseExpiresAtUtc,
            CompletedAtUtc = entity.CompletedAtUtc,
            ErrorCode = entity.ErrorCode,
            ErrorMessage = entity.ErrorMessage,
            LastResultJson = entity.LastResultJson
        };

    private static BackgroundJobSnapshot Map(DbDataReader reader)
        => new()
        {
            Id = reader.GetGuid(0),
            CompanyId = reader.GetGuid(1),
            CreatedByUserId = ReadNullableString(reader, 2),
            CreatedByEmail = ReadNullableString(reader, 3),
            JobType = reader.GetString(4),
            Status = ParseStatus(reader.GetString(5)),
            CorrelationKey = ReadNullableString(reader, 6),
            PayloadJson = reader.GetString(7),
            AttemptCount = reader.GetInt32(8),
            MaxAttempts = reader.GetInt32(9),
            CreatedAtUtc = reader.GetDateTime(10),
            UpdatedAtUtc = reader.GetDateTime(11),
            QueuedAtUtc = reader.GetDateTime(12),
            AvailableAtUtc = reader.GetDateTime(13),
            StartedAtUtc = ReadNullableDateTime(reader, 14),
            LastAttemptAtUtc = ReadNullableDateTime(reader, 15),
            ClaimedBy = ReadNullableString(reader, 16),
            ClaimedAtUtc = ReadNullableDateTime(reader, 17),
            LastHeartbeatAtUtc = ReadNullableDateTime(reader, 18),
            LeaseExpiresAtUtc = ReadNullableDateTime(reader, 19),
            CompletedAtUtc = ReadNullableDateTime(reader, 20),
            ErrorCode = ReadNullableString(reader, 21),
            ErrorMessage = ReadNullableString(reader, 22),
            LastResultJson = ReadNullableString(reader, 23)
        };

    private static BackgroundJobStatus ParseStatus(string? status)
        => Enum.TryParse<BackgroundJobStatus>(status, ignoreCase: false, out var parsed)
            ? parsed
            : BackgroundJobStatus.Queued;

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? NormalizeLarge(string? value)
        => Normalize(value, 8000);

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string? ReadNullableString(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static DateTime? ReadNullableDateTime(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
}

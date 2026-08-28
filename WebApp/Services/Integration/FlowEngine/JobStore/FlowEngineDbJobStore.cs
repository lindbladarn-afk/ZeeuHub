using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebApp.Data;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineDbJobStore : IFlowEngineJobStore
{
    private static readonly JsonSerializerOptions JsonOptions = new();
    private static readonly FlowEngineInMemoryJobStore FallbackStore = new();

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<FlowEngineDbJobStore> _logger;

    public FlowEngineDbJobStore(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<FlowEngineDbJobStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public FlowEngineJobSnapshot Create(Guid companyId, string? userId, string? userName, string[] arguments, FlowEngineExecuteJobRequest request)
    {
        try
        {
            using var db = _dbContextFactory.CreateDbContext();

            var entity = new FlowEngineJobRecord
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                UserId = userId,
                UserName = Normalize(userName, 256),
                Name = Normalize(request.Name, 128) ?? request.Operation.ToString(),
                UiLabel = Normalize(request.UiLabel, 128) ?? request.Operation.ToString(),
                IsScheduled = false,
                Status = FlowEngineJobStatus.Queued.ToString(),
                ArgumentsJson = JsonSerializer.Serialize(arguments, JsonOptions),
                RequestJson = JsonSerializer.Serialize(request, JsonOptions),
                CreatedAtUtc = DateTime.UtcNow
            };

            db.FlowEngineJobs!.Add(entity);
            db.SaveChanges();
            return Map(entity);
        }
        catch (Exception ex)
        {
            return Fallback("create", ex, () => FallbackStore.Create(companyId, userId, userName, arguments, request));
        }
    }

    public FlowEngineJobSnapshot MarkRunning(Guid companyId, Guid jobId, DateTimeOffset startedAtUtc)
        => TryUpdate(
            "mark-running",
            () => Update(companyId, jobId, entity =>
            {
                entity.Status = FlowEngineJobStatus.Running.ToString();
                entity.StartedAtUtc = startedAtUtc.UtcDateTime;
            }),
            () => FallbackStore.MarkRunning(companyId, jobId, startedAtUtc));

    public FlowEngineJobSnapshot Complete(Guid companyId, Guid jobId, FlowEngineJobResultPayload result)
        => TryUpdate(
            "complete",
            () => Update(companyId, jobId, entity =>
            {
                entity.Status = FlowEngineJobStatus.Succeeded.ToString();
                entity.StartedAtUtc ??= result.StartedAtUtc.UtcDateTime;
                entity.FinishedAtUtc = result.FinishedAtUtc.UtcDateTime;
                entity.ResultCommandLine = Truncate(result.CommandLine, 512);
                entity.ResultExitCode = result.ExitCode;
                entity.ResultSucceeded = result.Succeeded;
                entity.ResultStandardOutput = result.StandardOutput;
                entity.ResultStandardError = result.StandardError;
                entity.ErrorMessage = null;
            }),
            () => FallbackStore.Complete(companyId, jobId, result));

    public FlowEngineJobSnapshot Fail(Guid companyId, Guid jobId, FlowEngineJobResultPayload result, string errorMessage)
        => TryUpdate(
            "fail",
            () => Update(companyId, jobId, entity =>
            {
                entity.Status = FlowEngineJobStatus.Failed.ToString();
                entity.StartedAtUtc ??= result.StartedAtUtc.UtcDateTime;
                entity.FinishedAtUtc = result.FinishedAtUtc.UtcDateTime;
                entity.ResultCommandLine = Truncate(result.CommandLine, 512);
                entity.ResultExitCode = result.ExitCode;
                entity.ResultSucceeded = result.Succeeded;
                entity.ResultStandardOutput = result.StandardOutput;
                entity.ResultStandardError = result.StandardError;
                entity.ErrorMessage = errorMessage;
            }),
            () => FallbackStore.Fail(companyId, jobId, result, errorMessage));

    public FlowEngineJobSnapshot? Get(Guid companyId, Guid jobId)
    {
        try
        {
            using var db = _dbContextFactory.CreateDbContext();
            var entity = db.FlowEngineJobs!
                .AsNoTracking()
                .FirstOrDefault(job => job.CompanyId == companyId && job.Id == jobId);

            return entity is null ? null : Map(entity);
        }
        catch (Exception ex)
        {
            return Fallback("get", ex, () => FallbackStore.Get(companyId, jobId));
        }
    }

    public IReadOnlyList<FlowEngineJobSnapshot> ListRecent(Guid companyId, int take)
    {
        try
        {
            var safeTake = take <= 0 ? 10 : take;

            using var db = _dbContextFactory.CreateDbContext();
            return db.FlowEngineJobs!
                .AsNoTracking()
                .Where(job => job.CompanyId == companyId)
                .OrderByDescending(job => job.CreatedAtUtc)
                .Take(safeTake)
                .Select(job => new FlowEngineJobSnapshot
                {
                    Id = job.Id,
                    Name = job.Name,
                    UiLabel = job.UiLabel,
                    IsScheduled = job.IsScheduled,
                    Status = job.Status == nameof(FlowEngineJobStatus.Succeeded)
                        ? FlowEngineJobStatus.Succeeded
                        : job.Status == nameof(FlowEngineJobStatus.Failed)
                            ? FlowEngineJobStatus.Failed
                            : job.Status == nameof(FlowEngineJobStatus.Running)
                                ? FlowEngineJobStatus.Running
                                : job.Status == nameof(FlowEngineJobStatus.Cancelled)
                                    ? FlowEngineJobStatus.Cancelled
                                    : FlowEngineJobStatus.Queued,
                    Arguments = DeserializeArguments(job.ArgumentsJson),
                    CreatedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(job.CreatedAtUtc, DateTimeKind.Utc)),
                    StartedAtUtc = job.StartedAtUtc.HasValue
                        ? new DateTimeOffset(DateTime.SpecifyKind(job.StartedAtUtc.Value, DateTimeKind.Utc))
                        : null,
                    FinishedAtUtc = job.FinishedAtUtc.HasValue
                        ? new DateTimeOffset(DateTime.SpecifyKind(job.FinishedAtUtc.Value, DateTimeKind.Utc))
                        : null,
                    Result = null,
                    ErrorMessage = job.ErrorMessage,
                    StorageKind = FlowEngineJobStorageKind.Persistent,
                    StorageWarning = null
                })
                .ToList();
        }
        catch (Exception ex)
        {
            return Fallback("list-recent", ex, () => FallbackStore.ListRecent(companyId, take));
        }
    }

    public FlowEngineHistoryPageResult ListPage(Guid companyId, int page, int pageSize, string? systemKey = null, FlowEngineHistoryFilterState? filters = null)
    {
        try
        {
            var safePageSize = pageSize <= 0 ? 15 : pageSize;
            var safePage = page <= 0 ? 1 : page;
            var normalizedFilters = NormalizeFilters(filters);

            using var db = _dbContextFactory.CreateDbContext();
            var summaries = db.FlowEngineJobs!
                .AsNoTracking()
                .Where(job => job.CompanyId == companyId)
                .OrderByDescending(job => job.CreatedAtUtc)
                .Select(job => new FlowEngineJobSnapshot
                {
                    Id = job.Id,
                    Name = job.Name,
                    UiLabel = job.UiLabel,
                    IsScheduled = job.IsScheduled,
                    Status = job.Status == nameof(FlowEngineJobStatus.Succeeded)
                        ? FlowEngineJobStatus.Succeeded
                        : job.Status == nameof(FlowEngineJobStatus.Failed)
                            ? FlowEngineJobStatus.Failed
                            : job.Status == nameof(FlowEngineJobStatus.Running)
                                ? FlowEngineJobStatus.Running
                                : job.Status == nameof(FlowEngineJobStatus.Cancelled)
                                    ? FlowEngineJobStatus.Cancelled
                                    : FlowEngineJobStatus.Queued,
                    Arguments = DeserializeArguments(job.ArgumentsJson),
                    CreatedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(job.CreatedAtUtc, DateTimeKind.Utc)),
                    StartedAtUtc = job.StartedAtUtc.HasValue
                        ? new DateTimeOffset(DateTime.SpecifyKind(job.StartedAtUtc.Value, DateTimeKind.Utc))
                        : null,
                    FinishedAtUtc = job.FinishedAtUtc.HasValue
                        ? new DateTimeOffset(DateTime.SpecifyKind(job.FinishedAtUtc.Value, DateTimeKind.Utc))
                        : null,
                    Result = null,
                    ErrorMessage = job.ErrorMessage,
                    StorageKind = FlowEngineJobStorageKind.Persistent,
                    StorageWarning = null
                })
                .ToList();

            var systemScoped = string.IsNullOrWhiteSpace(systemKey)
                ? summaries
                : summaries.Where(job => MatchesSystem(job, systemKey)).ToList();
            var availableSystems = systemScoped
                .Select(FlowEngineJobPresentation.GetSystemLabel)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var availableOperations = systemScoped
                .Select(job => job.UiLabel)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var availableStatuses = systemScoped
                .Select(job => FlowEngineJobPresentation.GetStatusLabel(job.Status))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var filtered = systemScoped
                .Where(job => MatchesFilters(job, normalizedFilters))
                .ToList();
            var totalCount = filtered.Count;
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)safePageSize);
            var boundedPage = totalPages == 0 ? 1 : Math.Min(safePage, totalPages);

            return new FlowEngineHistoryPageResult
            {
                Jobs = filtered
                    .Skip((boundedPage - 1) * safePageSize)
                    .Take(safePageSize)
                    .ToList(),
                CurrentPage = boundedPage,
                TotalPages = totalPages,
                TotalCount = totalCount,
                PageSize = safePageSize,
                AvailableSystems = availableSystems,
                AvailableOperations = availableOperations,
                AvailableStatuses = availableStatuses,
                Filters = normalizedFilters
            };
        }
        catch (Exception ex)
        {
            return Fallback("list-page", ex, () => FallbackStore.ListPage(companyId, page, pageSize, systemKey, filters));
        }
    }

    private FlowEngineJobSnapshot Update(Guid companyId, Guid jobId, Action<FlowEngineJobRecord> mutate)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var entity = db.FlowEngineJobs!
            .FirstOrDefault(job => job.CompanyId == companyId && job.Id == jobId)
            ?? throw new InvalidOperationException($"FlowEngine job '{jobId}' could not be found.");

        mutate(entity);
        db.SaveChanges();
        return Map(entity);
    }

    private static FlowEngineJobSnapshot Map(FlowEngineJobRecord entity)
    {
        var status = Enum.TryParse<FlowEngineJobStatus>(entity.Status, out var parsedStatus)
            ? parsedStatus
            : FlowEngineJobStatus.Queued;

        return new FlowEngineJobSnapshot
        {
            Id = entity.Id,
            Name = entity.Name,
            UiLabel = entity.UiLabel,
            IsScheduled = entity.IsScheduled,
            Status = status,
            Arguments = DeserializeArguments(entity.ArgumentsJson),
            CreatedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAtUtc, DateTimeKind.Utc)),
            StartedAtUtc = entity.StartedAtUtc.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(entity.StartedAtUtc.Value, DateTimeKind.Utc))
                : null,
            FinishedAtUtc = entity.FinishedAtUtc.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(entity.FinishedAtUtc.Value, DateTimeKind.Utc))
                : null,
            Result = entity.ResultExitCode.HasValue
                ? new FlowEngineJobResultPayload
                {
                    CommandLine = entity.ResultCommandLine ?? string.Empty,
                    ExitCode = entity.ResultExitCode.Value,
                    Succeeded = entity.ResultSucceeded ?? false,
                    StandardOutput = entity.ResultStandardOutput ?? string.Empty,
                    StandardError = entity.ResultStandardError ?? string.Empty,
                    StartedAtUtc = entity.StartedAtUtc.HasValue
                        ? new DateTimeOffset(DateTime.SpecifyKind(entity.StartedAtUtc.Value, DateTimeKind.Utc))
                        : new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAtUtc, DateTimeKind.Utc)),
                    FinishedAtUtc = entity.FinishedAtUtc.HasValue
                        ? new DateTimeOffset(DateTime.SpecifyKind(entity.FinishedAtUtc.Value, DateTimeKind.Utc))
                        : new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAtUtc, DateTimeKind.Utc))
                }
                : null,
            ErrorMessage = entity.ErrorMessage,
            StorageKind = FlowEngineJobStorageKind.Persistent,
            StorageWarning = null
        };
    }

    private static List<string> DeserializeArguments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static bool MatchesSystem(FlowEngineJobSnapshot job, string systemKey)
        => string.Equals(job.Arguments.FirstOrDefault(), systemKey, StringComparison.OrdinalIgnoreCase);

    private static FlowEngineHistoryFilterState NormalizeFilters(FlowEngineHistoryFilterState? filters)
        => new()
        {
            System = NormalizeFilterValue(filters?.System),
            Operation = NormalizeFilterValue(filters?.Operation),
            Status = NormalizeFilterValue(filters?.Status),
            DateStart = NormalizeFilterValue(filters?.DateStart),
            DateEnd = NormalizeFilterValue(filters?.DateEnd)
        };

    private static string? NormalizeFilterValue(string? value)
        => string.IsNullOrWhiteSpace(value) || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)
            ? null
            : value.Trim();

    private static bool MatchesFilters(FlowEngineJobSnapshot job, FlowEngineHistoryFilterState filters)
    {
        var systemLabel = FlowEngineJobPresentation.GetSystemLabel(job);
        var statusLabel = FlowEngineJobPresentation.GetStatusLabel(job.Status);
        var date = job.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd");

        if (!string.IsNullOrWhiteSpace(filters.System) && !string.Equals(systemLabel, filters.System, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(filters.Operation) && !string.Equals(job.UiLabel, filters.Operation, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(filters.Status) && !string.Equals(statusLabel, filters.Status, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(filters.DateStart) && string.CompareOrdinal(date, filters.DateStart) < 0)
            return false;

        if (!string.IsNullOrWhiteSpace(filters.DateEnd) && string.CompareOrdinal(date, filters.DateEnd) > 0)
            return false;

        return true;
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Truncate(value.Trim(), maxLength);
    }

    private FlowEngineJobSnapshot TryUpdate(string operation, Func<FlowEngineJobSnapshot> primary, Func<FlowEngineJobSnapshot> fallback)
    {
        try
        {
            return primary();
        }
        catch (Exception ex)
        {
            return Fallback(operation, ex, fallback);
        }
    }

    private T Fallback<T>(string operation, Exception exception, Func<T> fallback)
    {
        _logger.LogWarning(
            exception,
            "FlowEngine DB job store failed during {Operation}. Falling back to in-memory job store.",
            operation);
        return fallback();
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}

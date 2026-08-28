using System.Collections.Concurrent;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineInMemoryJobStore : IFlowEngineJobStore
{
    private const int MaxJobsPerCompany = 30;

    private readonly ConcurrentDictionary<Guid, List<FlowEngineJobSnapshot>> _jobsByCompany = new();
    private readonly ConcurrentDictionary<Guid, object> _companyLocks = new();

    public FlowEngineJobSnapshot Create(Guid companyId, string? userId, string? userName, string[] arguments, FlowEngineExecuteJobRequest request)
    {
        var snapshot = new FlowEngineJobSnapshot
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(request.Name) ? request.Operation.ToString() : request.Name.Trim(),
            UiLabel = string.IsNullOrWhiteSpace(request.UiLabel) ? request.Operation.ToString() : request.UiLabel.Trim(),
            IsScheduled = false,
            Status = FlowEngineJobStatus.Queued,
            Arguments = arguments.ToList(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ErrorMessage = null,
            StorageKind = FlowEngineJobStorageKind.InMemoryFallback,
            StorageWarning = "History is currently running in temporary in-memory fallback mode."
        };

        var gate = _companyLocks.GetOrAdd(companyId, static _ => new object());
        lock (gate)
        {
            var jobs = _jobsByCompany.GetOrAdd(companyId, static _ => new List<FlowEngineJobSnapshot>());
            jobs.Insert(0, snapshot);
            if (jobs.Count > MaxJobsPerCompany)
                jobs.RemoveRange(MaxJobsPerCompany, jobs.Count - MaxJobsPerCompany);
        }

        return Clone(snapshot);
    }

    public FlowEngineJobSnapshot MarkRunning(Guid companyId, Guid jobId, DateTimeOffset startedAtUtc)
    {
        return Update(companyId, jobId, snapshot =>
        {
            snapshot.Status = FlowEngineJobStatus.Running;
            snapshot.StartedAtUtc = startedAtUtc;
        });
    }

    public FlowEngineJobSnapshot Complete(Guid companyId, Guid jobId, FlowEngineJobResultPayload result)
    {
        return Update(companyId, jobId, snapshot =>
        {
            snapshot.Status = FlowEngineJobStatus.Succeeded;
            snapshot.StartedAtUtc ??= result.StartedAtUtc;
            snapshot.FinishedAtUtc = result.FinishedAtUtc;
            snapshot.Result = Clone(result);
            snapshot.ErrorMessage = null;
        });
    }

    public FlowEngineJobSnapshot Fail(Guid companyId, Guid jobId, FlowEngineJobResultPayload result, string errorMessage)
    {
        return Update(companyId, jobId, snapshot =>
        {
            snapshot.Status = FlowEngineJobStatus.Failed;
            snapshot.StartedAtUtc ??= result.StartedAtUtc;
            snapshot.FinishedAtUtc = result.FinishedAtUtc;
            snapshot.Result = Clone(result);
            snapshot.ErrorMessage = errorMessage;
        });
    }

    public FlowEngineJobSnapshot? Get(Guid companyId, Guid jobId)
    {
        var gate = _companyLocks.GetOrAdd(companyId, static _ => new object());
        lock (gate)
        {
            if (!_jobsByCompany.TryGetValue(companyId, out var jobs))
                return null;

            return Clone(jobs.FirstOrDefault(job => job.Id == jobId));
        }
    }

    public IReadOnlyList<FlowEngineJobSnapshot> ListRecent(Guid companyId, int take)
    {
        var safeTake = take <= 0 ? 10 : take;
        var gate = _companyLocks.GetOrAdd(companyId, static _ => new object());
        lock (gate)
        {
            if (!_jobsByCompany.TryGetValue(companyId, out var jobs))
                return Array.Empty<FlowEngineJobSnapshot>();

            return jobs.Take(safeTake).Select(CloneSummary).ToList();
        }
    }

    public FlowEngineHistoryPageResult ListPage(Guid companyId, int page, int pageSize, string? systemKey = null, FlowEngineHistoryFilterState? filters = null)
    {
        var safePageSize = pageSize <= 0 ? 15 : pageSize;
        var safePage = page <= 0 ? 1 : page;
        var normalizedFilters = NormalizeFilters(filters);
        var gate = _companyLocks.GetOrAdd(companyId, static _ => new object());
        lock (gate)
        {
            if (!_jobsByCompany.TryGetValue(companyId, out var jobs))
            {
                return new FlowEngineHistoryPageResult
                {
                    CurrentPage = safePage,
                    PageSize = safePageSize,
                    Filters = normalizedFilters
                };
            }

            var systemScoped = string.IsNullOrWhiteSpace(systemKey)
                ? jobs
                : jobs.Where(job => MatchesSystem(job, systemKey)).ToList();
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
                    .Select(CloneSummary)
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
    }

    private FlowEngineJobSnapshot Update(Guid companyId, Guid jobId, Action<FlowEngineJobSnapshot> mutate)
    {
        var gate = _companyLocks.GetOrAdd(companyId, static _ => new object());
        lock (gate)
        {
            var jobs = _jobsByCompany.GetOrAdd(companyId, static _ => new List<FlowEngineJobSnapshot>());
            var snapshot = jobs.FirstOrDefault(job => job.Id == jobId)
                ?? throw new InvalidOperationException($"FlowEngine job '{jobId}' could not be found.");

            mutate(snapshot);
            return Clone(snapshot);
        }
    }

    private static FlowEngineJobSnapshot Clone(FlowEngineJobSnapshot? snapshot)
    {
        if (snapshot is null)
            return null!;

        return new FlowEngineJobSnapshot
        {
            Id = snapshot.Id,
            Name = snapshot.Name,
            UiLabel = snapshot.UiLabel,
            IsScheduled = snapshot.IsScheduled,
            Status = snapshot.Status,
            Arguments = snapshot.Arguments.ToList(),
            CreatedAtUtc = snapshot.CreatedAtUtc,
            StartedAtUtc = snapshot.StartedAtUtc,
            FinishedAtUtc = snapshot.FinishedAtUtc,
            Result = snapshot.Result is null ? null : Clone(snapshot.Result),
            ErrorMessage = snapshot.ErrorMessage,
            StorageKind = snapshot.StorageKind,
            StorageWarning = snapshot.StorageWarning
        };
    }

    private static FlowEngineJobResultPayload Clone(FlowEngineJobResultPayload result)
    {
        return new FlowEngineJobResultPayload
        {
            CommandLine = result.CommandLine,
            ExitCode = result.ExitCode,
            Succeeded = result.Succeeded,
            StandardOutput = result.StandardOutput,
            StandardError = result.StandardError,
            StartedAtUtc = result.StartedAtUtc,
            FinishedAtUtc = result.FinishedAtUtc
        };
    }

    private static FlowEngineJobSnapshot CloneSummary(FlowEngineJobSnapshot snapshot)
    {
        return new FlowEngineJobSnapshot
        {
            Id = snapshot.Id,
            Name = snapshot.Name,
            UiLabel = snapshot.UiLabel,
            IsScheduled = snapshot.IsScheduled,
            Status = snapshot.Status,
            Arguments = snapshot.Arguments.ToList(),
            CreatedAtUtc = snapshot.CreatedAtUtc,
            StartedAtUtc = snapshot.StartedAtUtc,
            FinishedAtUtc = snapshot.FinishedAtUtc,
            Result = null,
            ErrorMessage = snapshot.ErrorMessage,
            StorageKind = snapshot.StorageKind,
            StorageWarning = snapshot.StorageWarning
        };
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
}

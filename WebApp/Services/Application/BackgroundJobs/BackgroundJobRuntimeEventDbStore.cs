using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.BackgroundJobs;

namespace WebApp.Services.Application.BackgroundJobs;

public sealed class BackgroundJobRuntimeEventDbStore : IBackgroundJobRuntimeEventStore
{
    private const int MaxFallbackEventsPerCompany = 24;
    private static readonly ConcurrentDictionary<Guid, List<BackgroundJobRuntimeEventRecord>> FallbackEvents = new();
    private static readonly ConcurrentDictionary<Guid, object> FallbackLocks = new();

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<BackgroundJobRuntimeEventDbStore> _logger;

    public BackgroundJobRuntimeEventDbStore(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<BackgroundJobRuntimeEventDbStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public void Record(BackgroundJobRuntimeEventRecord record)
    {
        try
        {
            using var db = _dbContextFactory.CreateDbContext();
            db.BackgroundJobRuntimeEvents!.Add(record);
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background job runtime event store fell back to in-memory record storage.");
            RecordFallback(record);
        }
    }

    public IReadOnlyList<BackgroundJobRuntimeEventRecord> ListRecent(Guid companyId, int take)
    {
        var safeTake = take <= 0 ? 10 : take;

        try
        {
            using var db = _dbContextFactory.CreateDbContext();
            return db.BackgroundJobRuntimeEvents!
                .AsNoTracking()
                .Where(item => item.CompanyId == companyId)
                .OrderByDescending(item => item.OccurredAtUtc)
                .Take(safeTake)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background job runtime event store fell back to in-memory read storage.");
            return ListFallback(companyId, safeTake);
        }
    }

    private static void RecordFallback(BackgroundJobRuntimeEventRecord record)
    {
        var eventLock = FallbackLocks.GetOrAdd(record.CompanyId, _ => new object());
        lock (eventLock)
        {
            var events = FallbackEvents.GetOrAdd(record.CompanyId, _ => new List<BackgroundJobRuntimeEventRecord>());
            events.Insert(0, Clone(record));

            if (events.Count > MaxFallbackEventsPerCompany)
            {
                events.RemoveRange(MaxFallbackEventsPerCompany, events.Count - MaxFallbackEventsPerCompany);
            }
        }
    }

    private static IReadOnlyList<BackgroundJobRuntimeEventRecord> ListFallback(Guid companyId, int take)
    {
        if (!FallbackEvents.TryGetValue(companyId, out var events))
        {
            return Array.Empty<BackgroundJobRuntimeEventRecord>();
        }

        return events
            .OrderByDescending(item => item.OccurredAtUtc)
            .Take(take)
            .Select(Clone)
            .ToList();
    }

    private static BackgroundJobRuntimeEventRecord Clone(BackgroundJobRuntimeEventRecord record)
        => new()
        {
            Id = record.Id,
            JobId = record.JobId,
            CompanyId = record.CompanyId,
            EventType = record.EventType,
            AggregateKey = record.AggregateKey,
            Source = record.Source,
            Title = record.Title,
            StatusLabel = record.StatusLabel,
            StatusTone = record.StatusTone,
            IconClass = record.IconClass,
            LinkUrl = record.LinkUrl,
            Summary = record.Summary,
            OccurredAtUtc = record.OccurredAtUtc
        };
}

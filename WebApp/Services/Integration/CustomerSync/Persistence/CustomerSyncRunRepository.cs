using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Integration.CustomerSync;
using WebApp.Services.Integration.CustomerSync.Domain;

namespace WebApp.Services.Integration.CustomerSync.Persistence;

// Records sync runs and per-customer outcomes for operational traceability.
public sealed class CustomerSyncRunRepository : ICustomerSyncRunRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public CustomerSyncRunRepository(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<CustomerSyncRunRecord> StartAsync(
        Guid companyId,
        int jeevesCompanyCode,
        CustomerSyncDirection direction,
        CustomerSyncTrigger trigger,
        string? correlationId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var entity = new CustomerSyncRunRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            JeevesCompanyCode = jeevesCompanyCode,
            Direction = direction.ToString(),
            Trigger = trigger.ToString(),
            Status = CustomerSyncStatus.Running.ToString(),
            StartedAtUtc = utcNow,
            CorrelationId = Normalize(correlationId, 128)
        };

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.CustomerSyncRuns!.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task AddItemAsync(
        CustomerSyncRunItemRecord item,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
        item.ExternalKey = Normalize(item.ExternalKey, 128);
        item.JeevesCustomerNumber = Normalize(item.JeevesCustomerNumber, 64);
        item.HubSpotObjectId = Normalize(item.HubSpotObjectId, 64);
        item.ErrorCode = Normalize(item.ErrorCode, 64);
        item.ErrorMessage = Normalize(item.ErrorMessage, 1000);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.CustomerSyncRunItems!.Add(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CustomerSyncRunRecord> FinishAsync(
        Guid runId,
        CustomerSyncStatus status,
        int createdCount,
        int updatedCount,
        int skippedCount,
        int failedCount,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.CustomerSyncRuns!
            .FirstOrDefaultAsync(item => item.Id == runId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer sync run '{runId}' was not found.");

        entity.Status = status.ToString();
        entity.FinishedAtUtc = utcNow;
        entity.CreatedCount = Math.Max(0, createdCount);
        entity.UpdatedCount = Math.Max(0, updatedCount);
        entity.SkippedCount = Math.Max(0, skippedCount);
        entity.FailedCount = Math.Max(0, failedCount);

        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

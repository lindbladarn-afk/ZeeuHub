using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Integration.CustomerSync;
using WebApp.Services.Integration.CustomerSync.Domain;

namespace WebApp.Services.Integration.CustomerSync.Persistence;

// Stores HubSpot webhook events before processing so duplicate deliveries are harmless.
public sealed class CustomerSyncEventRepository : ICustomerSyncEventRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public CustomerSyncEventRepository(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<CustomerSyncEventRecord> RecordHubSpotEventAsync(
        Guid companyId,
        string hubSpotEventId,
        string? hubSpotObjectId,
        string? eventType,
        string? payloadHash,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hubSpotEventId))
            throw new ArgumentException("HubSpot event id is required.", nameof(hubSpotEventId));

        var normalizedEventId = hubSpotEventId.Trim();
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.CustomerSyncEvents!
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.CompanyId == companyId && item.HubSpotEventId == normalizedEventId,
                cancellationToken);

        if (existing is not null)
            return existing;

        var entity = new CustomerSyncEventRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            HubSpotEventId = normalizedEventId,
            HubSpotObjectId = Normalize(hubSpotObjectId, 64),
            EventType = Normalize(eventType, 128),
            PayloadHash = Normalize(payloadHash, 128),
            ReceivedAtUtc = utcNow,
            Status = CustomerSyncStatus.Pending.ToString()
        };

        db.CustomerSyncEvents!.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return entity;
        }
        catch (DbUpdateException)
        {
            db.Entry(entity).State = EntityState.Detached;

            var duplicate = await db.CustomerSyncEvents!
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.CompanyId == companyId && item.HubSpotEventId == normalizedEventId,
                    cancellationToken);

            if (duplicate is not null)
                return duplicate;

            throw;
        }
    }

    public async Task MarkProcessedAsync(
        Guid companyId,
        string hubSpotEventId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await UpdateAsync(companyId, hubSpotEventId, entity =>
        {
            entity.Status = CustomerSyncStatus.Completed.ToString();
            entity.ProcessedAtUtc = utcNow;
            entity.ErrorMessage = null;
        }, cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid companyId,
        string hubSpotEventId,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        await UpdateAsync(companyId, hubSpotEventId, entity =>
        {
            entity.Status = CustomerSyncStatus.Failed.ToString();
            entity.ErrorMessage = Normalize(errorMessage, 1000);
        }, cancellationToken);
    }

    private async Task UpdateAsync(
        Guid companyId,
        string hubSpotEventId,
        Action<CustomerSyncEventRecord> mutate,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hubSpotEventId))
            throw new ArgumentException("HubSpot event id is required.", nameof(hubSpotEventId));

        var normalizedEventId = hubSpotEventId.Trim();
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.CustomerSyncEvents!
            .FirstOrDefaultAsync(
                item => item.CompanyId == companyId && item.HubSpotEventId == normalizedEventId,
                cancellationToken)
            ?? throw new InvalidOperationException($"Customer sync event '{normalizedEventId}' was not found.");

        mutate(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

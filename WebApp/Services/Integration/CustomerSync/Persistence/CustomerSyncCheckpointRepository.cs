using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Integration.CustomerSync;
using WebApp.Services.Integration.CustomerSync.Domain;

namespace WebApp.Services.Integration.CustomerSync.Persistence;

// Persists watermarks only after a sync batch has safely completed.
public sealed class CustomerSyncCheckpointRepository : ICustomerSyncCheckpointRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public CustomerSyncCheckpointRepository(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<CustomerSyncCheckpointRecord?> GetAsync(
        Guid companyId,
        int jeevesCompanyCode,
        CustomerSyncDirection direction,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var directionValue = direction.ToString();

        return await db.CustomerSyncCheckpoints!
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.CompanyId == companyId
                    && item.JeevesCompanyCode == jeevesCompanyCode
                    && item.Direction == directionValue,
                cancellationToken);
    }

    public async Task<CustomerSyncCheckpointRecord> UpsertAsync(
        Guid companyId,
        int jeevesCompanyCode,
        CustomerSyncDirection direction,
        string? checkpointValue,
        DateTime? checkpointUtc,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var directionValue = direction.ToString();
        var entity = await db.CustomerSyncCheckpoints!
            .FirstOrDefaultAsync(
                item => item.CompanyId == companyId
                    && item.JeevesCompanyCode == jeevesCompanyCode
                    && item.Direction == directionValue,
                cancellationToken);

        if (entity is null)
        {
            entity = new CustomerSyncCheckpointRecord
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                JeevesCompanyCode = jeevesCompanyCode,
                Direction = directionValue
            };
            db.CustomerSyncCheckpoints!.Add(entity);
        }

        entity.CheckpointValue = Normalize(checkpointValue, 256);
        entity.CheckpointUtc = checkpointUtc;
        entity.UpdatedAtUtc = utcNow;

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

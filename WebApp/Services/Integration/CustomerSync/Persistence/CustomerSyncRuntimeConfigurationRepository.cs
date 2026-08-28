using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Integration.CustomerSync;

namespace WebApp.Services.Integration.CustomerSync.Persistence;

// Stores the hub-managed CustomerSync runtime configuration as a single non-secret JSON blob.
public sealed class CustomerSyncRuntimeConfigurationRepository : ICustomerSyncRuntimeConfigurationRepository
{
    private const string DefaultConfigurationName = "Default";
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public CustomerSyncRuntimeConfigurationRepository(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<CustomerSyncRuntimeConfigurationRecord?> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.CustomerSyncRuntimeConfiguration!
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CustomerSyncRuntimeConfigurationRecord> UpsertAsync(CustomerSyncRuntimeConfigurationRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.CustomerSyncRuntimeConfiguration!
            .FirstOrDefaultAsync(item => item.ConfigurationName == DefaultConfigurationName, cancellationToken);

        var utcNow = DateTime.UtcNow;
        if (entity is null)
        {
            entity = new CustomerSyncRuntimeConfigurationRecord
            {
                Id = Guid.NewGuid(),
                ConfigurationName = DefaultConfigurationName,
                CreatedAtUtc = utcNow
            };
            db.CustomerSyncRuntimeConfiguration!.Add(entity);
        }

        entity.ConfigurationJson = record.ConfigurationJson;
        entity.UpdatedAtUtc = utcNow;

        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }
}

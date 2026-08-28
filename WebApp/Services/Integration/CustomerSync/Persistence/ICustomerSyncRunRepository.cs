using WebApp.Models.Integration.CustomerSync;
using WebApp.Services.Integration.CustomerSync.Domain;

namespace WebApp.Services.Integration.CustomerSync.Persistence;

public interface ICustomerSyncRunRepository
{
    Task<CustomerSyncRunRecord> StartAsync(
        Guid companyId,
        int jeevesCompanyCode,
        CustomerSyncDirection direction,
        CustomerSyncTrigger trigger,
        string? correlationId,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task AddItemAsync(
        CustomerSyncRunItemRecord item,
        CancellationToken cancellationToken);

    Task<CustomerSyncRunRecord> FinishAsync(
        Guid runId,
        CustomerSyncStatus status,
        int createdCount,
        int updatedCount,
        int skippedCount,
        int failedCount,
        DateTime utcNow,
        CancellationToken cancellationToken);
}

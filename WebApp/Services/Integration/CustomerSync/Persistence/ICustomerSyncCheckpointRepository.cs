using WebApp.Models.Integration.CustomerSync;
using WebApp.Services.Integration.CustomerSync.Domain;

namespace WebApp.Services.Integration.CustomerSync.Persistence;

public interface ICustomerSyncCheckpointRepository
{
    Task<CustomerSyncCheckpointRecord?> GetAsync(
        Guid companyId,
        int jeevesCompanyCode,
        CustomerSyncDirection direction,
        CancellationToken cancellationToken);

    Task<CustomerSyncCheckpointRecord> UpsertAsync(
        Guid companyId,
        int jeevesCompanyCode,
        CustomerSyncDirection direction,
        string? checkpointValue,
        DateTime? checkpointUtc,
        DateTime utcNow,
        CancellationToken cancellationToken);
}

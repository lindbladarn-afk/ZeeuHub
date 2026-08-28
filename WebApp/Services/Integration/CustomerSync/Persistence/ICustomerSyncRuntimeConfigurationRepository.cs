using WebApp.Models.Integration.CustomerSync;

namespace WebApp.Services.Integration.CustomerSync.Persistence;

public interface ICustomerSyncRuntimeConfigurationRepository
{
    Task<CustomerSyncRuntimeConfigurationRecord?> GetAsync(CancellationToken cancellationToken = default);
    Task<CustomerSyncRuntimeConfigurationRecord> UpsertAsync(CustomerSyncRuntimeConfigurationRecord record, CancellationToken cancellationToken = default);
}

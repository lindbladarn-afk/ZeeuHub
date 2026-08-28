using WebApp.Models.Integration.CustomerSync;

namespace WebApp.Services.Integration.CustomerSync.Application;

public interface ICustomerSyncRuntimeConfigurationService
{
    Task<CustomerSyncOptions> GetEffectiveOptionsAsync(CancellationToken cancellationToken = default);
    Task<CustomerSyncRuntimeConfiguration> GetRuntimeConfigurationAsync(CancellationToken cancellationToken = default);
    Task SaveRuntimeConfigurationAsync(CustomerSyncRuntimeConfiguration configuration, CancellationToken cancellationToken = default);
    Task<int> QueueManualRunsAsync(CancellationToken cancellationToken = default);
}

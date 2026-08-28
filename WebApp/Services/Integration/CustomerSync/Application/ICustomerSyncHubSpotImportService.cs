namespace WebApp.Services.Integration.CustomerSync.Application;

public interface ICustomerSyncHubSpotImportService
{
    Task<CustomerSyncHubSpotImportResult> ImportCompaniesAsync(CancellationToken cancellationToken = default);
}

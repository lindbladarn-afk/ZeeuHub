using WebApp.Models.Integration.CustomerSync;

namespace WebApp.Services.Integration.CustomerSync.Persistence;

public interface ICustomerSyncMappingRepository
{
    Task<CustomerSyncMappingRecord?> FindByJeevesCustomerAsync(
        Guid companyId,
        int jeevesCompanyCode,
        string jeevesCustomerNumber,
        CancellationToken cancellationToken);

    Task<CustomerSyncMappingRecord?> FindByHubSpotCompanyAsync(
        Guid companyId,
        string hubSpotCompanyId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerSyncMappingRecord>> FindByOrganizationNumberAsync(
        Guid companyId,
        string organizationNumber,
        CancellationToken cancellationToken);

    Task<int> CountHubSpotMappingsAsync(
        IReadOnlyCollection<Guid> companyIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerSyncMappingRecord>> ListHubSpotMappingsAsync(
        IReadOnlyCollection<Guid> companyIds,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<CustomerSyncMappingRecord> UpsertAsync(
        CustomerSyncMappingRecord mapping,
        DateTime utcNow,
        CancellationToken cancellationToken);
}

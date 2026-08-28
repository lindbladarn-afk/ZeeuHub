namespace WebApp.Services.Integration.CustomerSync.HubSpot;

public interface IHubSpotCustomerClient
{
    Task<IReadOnlyList<HubSpotCustomerDto>> ListCompaniesAsync(
        CustomerSyncHubSpotConnection connection,
        int limit,
        CancellationToken cancellationToken);

    Task<HubSpotCustomerDto?> GetCompanyAsync(
        Guid companyId,
        string hubSpotCompanyId,
        CancellationToken cancellationToken);

    Task<HubSpotCustomerWriteResult> UpsertCompanyAsync(
        Guid companyId,
        HubSpotCustomerDto customer,
        CancellationToken cancellationToken);
}

namespace WebApp.Services.Integration.CustomerSync.Jeeves;

public interface IJeevesCustomerSyncClient
{
    Task<IReadOnlyList<JeevesCustomerDto>> GetChangedCustomersAsync(
        Guid companyId,
        int jeevesCompanyCode,
        string? checkpointValue,
        DateTime? checkpointUtc,
        int take,
        CancellationToken cancellationToken);

    Task<JeevesCustomerWriteResult> UpsertCustomerAsync(
        Guid companyId,
        int jeevesCompanyCode,
        JeevesCustomerDto customer,
        CancellationToken cancellationToken);
}

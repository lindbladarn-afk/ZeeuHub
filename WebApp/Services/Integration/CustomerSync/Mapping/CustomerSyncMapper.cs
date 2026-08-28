using WebApp.Services.Integration.CustomerSync.Domain;

namespace WebApp.Services.Integration.CustomerSync.Mapping;

// Applies shared normalization so both sync directions use the same customer identity rules.
public sealed class CustomerSyncMapper : ICustomerSyncMapper
{
    private readonly ICustomerSyncNormalizer _normalizer;

    public CustomerSyncMapper(ICustomerSyncNormalizer normalizer)
    {
        _normalizer = normalizer;
    }

    public SyncedCustomer Normalize(SyncedCustomer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        return customer with
        {
            OrganizationNumber = _normalizer.NormalizeOrganizationNumber(customer.OrganizationNumber),
            Name = _normalizer.NormalizeName(customer.Name),
            Email = _normalizer.NormalizeEmail(customer.Email),
            Phone = _normalizer.NormalizePhone(customer.Phone)
        };
    }
}

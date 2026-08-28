using WebApp.Services.Integration.CustomerSync.Domain;

namespace WebApp.Services.Integration.CustomerSync.Mapping;

public interface ICustomerSyncMapper
{
    SyncedCustomer Normalize(SyncedCustomer customer);
}

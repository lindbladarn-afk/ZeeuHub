namespace WebApp.Services.Integration.CustomerSync.Domain;

// Keeps customer sync decisions deterministic and easy to unit test.
public sealed class CustomerSyncPolicy
{
    public CustomerSyncMatchDecision ValidateForSync(SyncedCustomer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        if (string.IsNullOrWhiteSpace(customer.Name)
            && string.IsNullOrWhiteSpace(customer.OrganizationNumber)
            && string.IsNullOrWhiteSpace(customer.Email))
        {
            return new CustomerSyncMatchDecision(
                CustomerSyncMatchDecisionKind.Invalid,
                "Customer needs at least name, organization number, or email.");
        }

        return new CustomerSyncMatchDecision(CustomerSyncMatchDecisionKind.NoMatch, null);
    }

    public CustomerSyncStatus ClassifyWriteResult(bool created, bool changed)
    {
        if (created)
            return CustomerSyncStatus.Created;

        return changed ? CustomerSyncStatus.Updated : CustomerSyncStatus.Skipped;
    }
}

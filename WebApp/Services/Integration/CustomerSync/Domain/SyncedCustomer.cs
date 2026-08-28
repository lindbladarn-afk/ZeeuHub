namespace WebApp.Services.Integration.CustomerSync.Domain;

// Represents the normalized customer shape used by sync policies and mappers.
public sealed record SyncedCustomer(
    string? JeevesCustomerNumber,
    string? HubSpotCompanyId,
    string? HubSpotContactId,
    string? OrganizationNumber,
    string? Name,
    string? Email,
    string? Phone,
    DateTime? UpdatedAtUtc);

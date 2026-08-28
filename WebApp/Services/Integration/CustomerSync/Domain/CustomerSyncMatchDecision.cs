namespace WebApp.Services.Integration.CustomerSync.Domain;

// Captures a matching decision without coupling domain rules to persistence.
public sealed record CustomerSyncMatchDecision(
    CustomerSyncMatchDecisionKind Kind,
    string? Reason,
    Guid? MappingId = null);

public enum CustomerSyncMatchDecisionKind
{
    NoMatch = 0,
    ExistingMapping = 1,
    OrganizationNumber = 2,
    Ambiguous = 3,
    Invalid = 4
}

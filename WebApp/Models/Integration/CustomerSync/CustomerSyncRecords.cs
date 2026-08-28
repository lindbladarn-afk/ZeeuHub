namespace WebApp.Models.Integration.CustomerSync;

// Stores stable cross-system customer identifiers so retries never need to guess.
public sealed class CustomerSyncMappingRecord
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public int JeevesCompanyCode { get; set; }
    public string? JeevesCustomerNumber { get; set; }
    public string? HubSpotCompanyId { get; set; }
    public string? HubSpotContactId { get; set; }
    public string? OrganizationNumber { get; set; }
    public string? NormalizedName { get; set; }
    public string? Domain { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? HubSpotUpdatedAtUtc { get; set; }
    public DateTime? LastSyncedFromJeevesAtUtc { get; set; }
    public DateTime? LastSyncedFromHubSpotAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

// Tracks the latest safe watermark for each customer sync direction.
public sealed class CustomerSyncCheckpointRecord
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public int JeevesCompanyCode { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string? CheckpointValue { get; set; }
    public DateTime? CheckpointUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

// Represents one customer sync batch for audit, retry, and support follow-up.
public sealed class CustomerSyncRunRecord
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public int JeevesCompanyCode { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string Trigger { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public string? CorrelationId { get; set; }
    public List<CustomerSyncRunItemRecord> Items { get; set; } = new();
}

// Stores the outcome for one customer inside a sync run.
public sealed class CustomerSyncRunItemRecord
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid CompanyId { get; set; }
    public string? ExternalKey { get; set; }
    public string? JeevesCustomerNumber { get; set; }
    public string? HubSpotObjectId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public CustomerSyncRunRecord? Run { get; set; }
}

// Stores inbound HubSpot webhook events idempotently before background processing.
public sealed class CustomerSyncEventRecord
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string HubSpotEventId { get; set; } = string.Empty;
    public string? HubSpotObjectId { get; set; }
    public string? EventType { get; set; }
    public string? PayloadHash { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

// Stores the hub-managed non-secret runtime settings for CustomerSync.
public sealed class CustomerSyncRuntimeConfigurationRecord
{
    public Guid Id { get; set; }
    public string ConfigurationName { get; set; } = "Default";
    public string ConfigurationJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

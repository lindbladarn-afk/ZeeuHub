namespace WebApp.Models.Integration.CustomerSync;

// Represents the non-secret runtime settings that the hub can manage for CustomerSync.
public sealed class CustomerSyncRuntimeConfiguration
{
    public bool Enabled { get; set; }
    public int PollIntervalMinutes { get; set; } = 60;
    public int BatchSize { get; set; } = 100;
    public int MaxAttempts { get; set; } = 5;
    public int WebhookToleranceMinutes { get; set; } = 5;
    public List<CustomerSyncRuntimeCompanyConfiguration> Companies { get; set; } = new();
}

public sealed class CustomerSyncRuntimeCompanyConfiguration
{
    public Guid CompanyId { get; set; }
    public int JeevesCompanyCode { get; set; }
    public bool Enabled { get; set; } = true;
    public string? HubSpotBaseUrl { get; set; }
}

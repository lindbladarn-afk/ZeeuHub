namespace WebApp.Models.Integration.CustomerSync;

// Defines runtime settings for the isolated Jeeves and HubSpot customer sync module.
public sealed class CustomerSyncOptions
{
    public const string SectionName = "CustomerSync";

    public bool Enabled { get; set; }
    public int PollIntervalMinutes { get; set; } = 60;
    public int BatchSize { get; set; } = 100;
    public int MaxAttempts { get; set; } = 5;
    public int WebhookToleranceMinutes { get; set; } = 5;
    public List<CustomerSyncCompanyOptions> Companies { get; set; } = new();
    public Dictionary<string, CustomerSyncCompanyOptions> NamedCompanies { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CustomerSyncCompanyOptions
{
    public Guid CompanyId { get; set; }
    public int JeevesCompanyCode { get; set; }
    public bool Enabled { get; set; } = true;
    public CustomerSyncHubSpotOptions HubSpot { get; set; } = new();
}

public sealed class CustomerSyncHubSpotOptions
{
    public string? BaseUrl { get; set; }
    public string? Token { get; set; }
    public string? WebhookSecret { get; set; }
}

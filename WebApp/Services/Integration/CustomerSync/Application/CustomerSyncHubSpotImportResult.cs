namespace WebApp.Services.Integration.CustomerSync.Application;

// Summarizes the read-only HubSpot company import shown on the CustomerSync page.
public sealed class CustomerSyncHubSpotImportResult
{
    public int ImportedCount { get; init; }
    public int SkippedCount { get; init; }
    public string Summary { get; init; } = string.Empty;
}

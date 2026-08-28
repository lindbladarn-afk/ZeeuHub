namespace WebApp.Services.Integration.CustomerSync.HubSpot;

// Carries non-persisted HubSpot connection details for one CustomerSync read operation.
public sealed class CustomerSyncHubSpotConnection
{
    public string? BaseUrl { get; init; }
    public string Token { get; init; } = string.Empty;
}

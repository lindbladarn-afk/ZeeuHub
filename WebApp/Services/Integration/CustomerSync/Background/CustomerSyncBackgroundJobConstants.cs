namespace WebApp.Services.Integration.CustomerSync.Background;

// Centralizes background-job identifiers used by CustomerSync.
public static class CustomerSyncBackgroundJobConstants
{
    public const string ExecuteJobType = "customersync.execute";
    public const int DefaultMaxAttempts = 5;
}

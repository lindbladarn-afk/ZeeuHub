namespace WebApp.Services.Integration.CustomerSync.Domain;

// Describes why a sync run was started.
public enum CustomerSyncTrigger
{
    Scheduled = 0,
    Webhook = 1,
    Manual = 2,
    Replay = 3
}

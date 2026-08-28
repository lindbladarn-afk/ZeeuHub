namespace WebApp.Services.Integration.CustomerSync.Domain;

// Represents durable status values stored in CustomerSync history tables.
public enum CustomerSyncStatus
{
    Pending = 0,
    Running = 1,
    Created = 2,
    Updated = 3,
    Skipped = 4,
    Failed = 5,
    Completed = 6,
    NeedsReview = 7
}

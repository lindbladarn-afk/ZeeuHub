namespace WebApp.Services.Integration.CustomerSync.Domain;

// Identifies the external system that currently owns a customer change.
public enum CustomerSyncExternalSystem
{
    Jeeves = 0,
    HubSpot = 1
}

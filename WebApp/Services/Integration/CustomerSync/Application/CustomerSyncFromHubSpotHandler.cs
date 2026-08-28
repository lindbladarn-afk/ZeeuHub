using WebApp.Services.Integration.CustomerSync.Domain;
using WebApp.Services.Integration.CustomerSync.Persistence;

namespace WebApp.Services.Integration.CustomerSync.Application;

// Orchestrates one HubSpot-to-Jeeves customer sync event for a single company.
public sealed class CustomerSyncFromHubSpotHandler
{
    private readonly ICustomerSyncEventRepository _eventRepository;
    private readonly ICustomerSyncRunRepository _runRepository;

    public CustomerSyncFromHubSpotHandler(
        ICustomerSyncEventRepository eventRepository,
        ICustomerSyncRunRepository runRepository)
    {
        _eventRepository = eventRepository;
        _runRepository = runRepository;
    }

    public async Task<CustomerSyncResult> ExecuteAsync(
        Guid companyId,
        int jeevesCompanyCode,
        string hubSpotEventId,
        string? hubSpotObjectId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var run = await _runRepository.StartAsync(
            companyId,
            jeevesCompanyCode,
            CustomerSyncDirection.HubSpotToJeeves,
            CustomerSyncTrigger.Webhook,
            correlationId,
            DateTime.UtcNow,
            cancellationToken);

        await _eventRepository.MarkProcessedAsync(companyId, hubSpotEventId, DateTime.UtcNow, cancellationToken);
        await _runRepository.FinishAsync(
            run.Id,
            CustomerSyncStatus.Completed,
            createdCount: 0,
            updatedCount: 0,
            skippedCount: 0,
            failedCount: 0,
            DateTime.UtcNow,
            cancellationToken);

        return new CustomerSyncResult
        {
            Succeeded = true,
            Summary = $"CustomerSync-händelsen för HubSpot-objektet '{hubSpotObjectId ?? "okänt"}' är klar. Jeeves-kopplingen är ännu inte aktiverad."
        };
    }
}

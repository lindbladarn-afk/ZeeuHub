using WebApp.Services.Integration.CustomerSync.Domain;
using WebApp.Services.Integration.CustomerSync.Persistence;

namespace WebApp.Services.Integration.CustomerSync.Application;

// Orchestrates one Jeeves-to-HubSpot customer sync batch for a single company.
public sealed class CustomerSyncFromJeevesHandler
{
    private readonly ICustomerSyncRunRepository _runRepository;

    public CustomerSyncFromJeevesHandler(ICustomerSyncRunRepository runRepository)
    {
        _runRepository = runRepository;
    }

    public async Task<CustomerSyncResult> ExecuteAsync(
        Guid companyId,
        int jeevesCompanyCode,
        CustomerSyncTrigger trigger,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var run = await _runRepository.StartAsync(
            companyId,
            jeevesCompanyCode,
            CustomerSyncDirection.JeevesToHubSpot,
            trigger,
            correlationId,
            utcNow,
            cancellationToken);

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
            Summary = "CustomerSync-steget är klart. Jeeves- och HubSpot-kopplingarna är ännu inte aktiverade."
        };
    }
}

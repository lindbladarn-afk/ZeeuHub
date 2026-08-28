using WebApp.Models.Integration.CustomerSync;
using WebApp.Services.Integration.CustomerSync.Domain;

namespace WebApp.Services.Integration.CustomerSync.Persistence;

public interface ICustomerSyncEventRepository
{
    Task<CustomerSyncEventRecord> RecordHubSpotEventAsync(
        Guid companyId,
        string hubSpotEventId,
        string? hubSpotObjectId,
        string? eventType,
        string? payloadHash,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task MarkProcessedAsync(
        Guid companyId,
        string hubSpotEventId,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        Guid companyId,
        string hubSpotEventId,
        string errorMessage,
        CancellationToken cancellationToken);
}

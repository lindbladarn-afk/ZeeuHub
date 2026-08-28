using System;
using System.Threading;
using System.Threading.Tasks;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration
{
    public interface IIntegrationSyncService
    {
        Task<IntegrationSyncResult> SyncCompanyAsync(Guid companyId, string? externalOrderId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default);
    }
}

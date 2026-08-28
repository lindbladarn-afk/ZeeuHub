using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebApp.Models.Integration;

namespace WebApp.Repositories.Integration
{
    public interface IIntegrationRepository
    {
        Task SaveOrdersAsync(Guid companyId, IntegrationSource source, IReadOnlyList<IntegrationOrder> orders, CancellationToken ct = default);
        Task SaveMatchesAsync(Guid companyId, IReadOnlyList<IntegrationMatch> matches, CancellationToken ct = default);
    }
}

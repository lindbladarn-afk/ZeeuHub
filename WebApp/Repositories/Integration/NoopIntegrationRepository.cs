using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebApp.Models.Integration;

namespace WebApp.Repositories.Integration
{
    public class NoopIntegrationRepository : IIntegrationRepository
    {
        private readonly ILogger<NoopIntegrationRepository> _logger;

        public NoopIntegrationRepository(ILogger<NoopIntegrationRepository> logger)
        {
            _logger = logger;
        }

        public Task SaveOrdersAsync(Guid companyId, IntegrationSource source, IReadOnlyList<IntegrationOrder> orders, CancellationToken ct = default)
        {
            _logger.LogInformation("Integration staging skipped (noop). Company {CompanyId} Source {Source} Orders {Count}",
                companyId, source, orders.Count);
            return Task.CompletedTask;
        }

        public Task SaveMatchesAsync(Guid companyId, IReadOnlyList<IntegrationMatch> matches, CancellationToken ct = default)
        {
            _logger.LogInformation("Integration matches skipped (noop). Company {CompanyId} Matches {Count}",
                companyId, matches.Count);
            return Task.CompletedTask;
        }
    }
}

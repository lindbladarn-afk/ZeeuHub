using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebApp.Models.Integration;
using WebApp.Repositories.Integration;

namespace WebApp.Services.Integration
{
    public class IntegrationSyncService : IIntegrationSyncService
    {
        private readonly IOptions<IntegrationOptions> _options;
        private readonly IEnumerable<IOrderSourceClient> _sources;
        private readonly IIntegrationMatcher _matcher;
        private readonly IIntegrationRepository _repository;
        private readonly ILogger<IntegrationSyncService> _logger;

        public IntegrationSyncService(
            IOptions<IntegrationOptions> options,
            IEnumerable<IOrderSourceClient> sources,
            IIntegrationMatcher matcher,
            IIntegrationRepository repository,
            ILogger<IntegrationSyncService> logger)
        {
            _options = options;
            _sources = sources;
            _matcher = matcher;
            _repository = repository;
            _logger = logger;
        }

        public async Task<IntegrationSyncResult> SyncCompanyAsync(Guid companyId, string? externalOrderId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
        {
            var result = new IntegrationSyncResult { CompanyId = companyId };
            var config = _options.Value.Companies.FirstOrDefault(c => c.CompanyId == companyId && c.Enabled);
            if (config == null)
            {
                result.Warnings.Add("No integration config found for company.");
                result.FinishedAtUtc = DateTime.UtcNow;
                return result;
            }

            IReadOnlyList<IntegrationOrder> centra = Array.Empty<IntegrationOrder>();
            IReadOnlyList<IntegrationOrder> jeeves = Array.Empty<IntegrationOrder>();
            IReadOnlyList<IntegrationOrder> ongoing = Array.Empty<IntegrationOrder>();
            List<string> missingInOngoing = new();

            try
            {
                centra = await FetchSourceAsync(IntegrationSource.Centra, config, externalOrderId, fromUtc, toUtc, ct);
            }
            catch (IntegrationSourceException ex)
            {
                result.Errors.Add(new IntegrationSourceError
                {
                    Source = ex.Source,
                    StatusCode = ex.StatusCode,
                    Message = ex.Message
                });
            }

            try
            {
                jeeves = await FetchSourceAsync(IntegrationSource.Jeeves, config, externalOrderId, fromUtc, toUtc, ct);
            }
            catch (IntegrationSourceException ex)
            {
                result.Errors.Add(new IntegrationSourceError
                {
                    Source = ex.Source,
                    StatusCode = ex.StatusCode,
                    Message = ex.Message
                });
            }

            result.CentraCount = centra.Count;
            result.JeevesCount = jeeves.Count;
            result.CentraOrders = centra.ToList();
            result.JeevesOrders = jeeves.ToList();

            try
            {
                var ongoingResult = await FetchOngoingAsync(config, jeeves, ct);
                ongoing = ongoingResult.Found;
                missingInOngoing = ongoingResult.Missing;
            }
            catch (IntegrationSourceException ex)
            {
                result.Errors.Add(new IntegrationSourceError
                {
                    Source = ex.Source,
                    StatusCode = ex.StatusCode,
                    Message = ex.Message
                });
            }

            result.OngoingCount = ongoing.Count;
            result.OngoingOrders = ongoing.ToList();
            result.MissingInOngoingCount = missingInOngoing.Count;
            result.MatchedOngoingOrderNos = ongoing
                .Select(o => o.OrderNo ?? o.ExternalId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            await _repository.SaveOrdersAsync(companyId, IntegrationSource.Centra, centra, ct);
            await _repository.SaveOrdersAsync(companyId, IntegrationSource.Jeeves, jeeves, ct);

            var matches = _matcher.Match(centra, jeeves, config);
            result.MissingInJeevesCount = matches.Count(m => m.MatchType == "MissingInJeeves");
            result.MatchedExternalIds = matches
                .Where(m => m.MatchType == "Exact" && !string.IsNullOrWhiteSpace(m.CentraOrderId))
                .Select(m => m.CentraOrderId)
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .ToList();
            await _repository.SaveMatchesAsync(companyId, matches, ct);

            result.FinishedAtUtc = DateTime.UtcNow;
            return result;
        }

        private async Task<IReadOnlyList<IntegrationOrder>> FetchSourceAsync(
            IntegrationSource source,
            IntegrationCompanyConfig config,
            string? externalOrderId,
            DateTime? fromUtc,
            DateTime? toUtc,
            CancellationToken ct)
        {
            var sourceConfig = config.GetSource(source);
            if (sourceConfig == null || !sourceConfig.Enabled || string.IsNullOrWhiteSpace(sourceConfig.BaseUrl))
                throw new IntegrationSourceException(source, null, "missing_config");

            if (source == IntegrationSource.Jeeves && string.IsNullOrWhiteSpace(sourceConfig.AuthUrl))
                throw new IntegrationSourceException(source, null, "missing_auth_url");

            var client = _sources.FirstOrDefault(s => s.Source == source);
            if (client == null)
            {
                _logger.LogWarning("No client registered for {Source}", source);
                throw new IntegrationSourceException(source, null, "missing_client");
            }

            var request = new IntegrationFetchRequest
            {
                CompanyId = config.CompanyId,
                JeevesCompanyCode = config.JeevesCompanyCode,
                ExternalOrderId = externalOrderId,
                FromUtc = fromUtc,
                ToUtc = toUtc
            };

            return await client.FetchOrdersAsync(request, ct);
        }

        private async Task<(IReadOnlyList<IntegrationOrder> Found, List<string> Missing)> FetchOngoingAsync(
            IntegrationCompanyConfig config,
            IReadOnlyList<IntegrationOrder> jeevesOrders,
            CancellationToken ct)
        {
            var sourceConfig = config.GetSource(IntegrationSource.Ongoing);
            if (sourceConfig == null || !sourceConfig.Enabled || string.IsNullOrWhiteSpace(sourceConfig.BaseUrl))
                return (Array.Empty<IntegrationOrder>(), new List<string>());

            var client = _sources.FirstOrDefault(s => s.Source == IntegrationSource.Ongoing);
            if (client == null)
            {
                _logger.LogWarning("No client registered for {Source}", IntegrationSource.Ongoing);
                throw new IntegrationSourceException(IntegrationSource.Ongoing, null, "missing_client");
            }

            var orderNos = jeevesOrders
                .Select(o => o.OrderNo)
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(50)
                .ToList();

            if (orderNos.Count == 0)
                return (Array.Empty<IntegrationOrder>(), new List<string>());

            var found = new List<IntegrationOrder>();
            var missing = new List<string>();

            foreach (var orderNo in orderNos)
            {
                ct.ThrowIfCancellationRequested();

                var request = new IntegrationFetchRequest
                {
                    CompanyId = config.CompanyId,
                    ExternalOrderId = orderNo,
                    JeevesCompanyCode = config.JeevesCompanyCode
                };

                var results = await client.FetchOrdersAsync(request, ct);
                if (results.Count > 0)
                {
                    found.AddRange(results);
                }
                else
                {
                    missing.Add(orderNo!);
                }
            }

            return (found, missing);
        }
    }
}

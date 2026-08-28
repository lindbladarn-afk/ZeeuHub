using System.Collections.Generic;
using System.Linq;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration
{
    public class IntegrationMatcher : IIntegrationMatcher
    {
        public IReadOnlyList<IntegrationMatch> Match(
            IReadOnlyList<IntegrationOrder> centraOrders,
            IReadOnlyList<IntegrationOrder> jeevesOrders,
            IntegrationCompanyConfig companyConfig)
        {
            var jeevesLookup = jeevesOrders
                .Where(o => !string.IsNullOrWhiteSpace(o.ExternalId))
                .ToDictionary(o => o.ExternalId, o => o, System.StringComparer.OrdinalIgnoreCase);

            var matches = new List<IntegrationMatch>();
            foreach (var centra in centraOrders)
            {
                if (string.IsNullOrWhiteSpace(centra.ExternalId))
                {
                    matches.Add(new IntegrationMatch
                    {
                        CompanyId = companyConfig.CompanyId,
                        CentraOrderId = string.Empty,
                        JeevesOrderId = null,
                        MatchType = "MissingExternalId"
                    });
                    continue;
                }

                jeevesLookup.TryGetValue(centra.ExternalId, out var jeeves);
                matches.Add(new IntegrationMatch
                {
                    CompanyId = companyConfig.CompanyId,
                    CentraOrderId = centra.ExternalId,
                    JeevesOrderId = jeeves?.ExternalId,
                    MatchType = jeeves is null ? "MissingInJeeves" : "Exact"
                });
            }

            return matches;
        }
    }
}

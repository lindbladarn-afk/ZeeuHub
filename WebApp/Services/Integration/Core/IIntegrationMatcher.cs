using System.Collections.Generic;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration
{
    public interface IIntegrationMatcher
    {
        IReadOnlyList<IntegrationMatch> Match(
            IReadOnlyList<IntegrationOrder> centraOrders,
            IReadOnlyList<IntegrationOrder> jeevesOrders,
            IntegrationCompanyConfig companyConfig);
    }
}

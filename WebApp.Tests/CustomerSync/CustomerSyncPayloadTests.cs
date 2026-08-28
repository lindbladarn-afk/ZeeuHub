using WebApp.Services.Integration.CustomerSync.Background;
using WebApp.Services.Integration.CustomerSync.Domain;

namespace WebApp.Tests.CustomerSync;

public sealed class CustomerSyncPayloadTests
{
    [Fact]
    public void Payload_Roundtrips_Direction_And_Correlation()
    {
        var payload = new CustomerSyncBackgroundJobPayload
        {
            CompanyId = Guid.NewGuid(),
            JeevesCompanyCode = 5,
            Direction = CustomerSyncDirection.HubSpotToJeeves,
            Trigger = CustomerSyncTrigger.Webhook,
            HubSpotEventId = "event-1",
            HubSpotObjectId = "company-1",
            CorrelationKey = "customersync:test"
        };

        var result = CustomerSyncBackgroundJobPayload.FromJson(payload.ToJson());

        Assert.Equal(payload.CompanyId, result.CompanyId);
        Assert.Equal(payload.JeevesCompanyCode, result.JeevesCompanyCode);
        Assert.Equal(payload.Direction, result.Direction);
        Assert.Equal(payload.Trigger, result.Trigger);
        Assert.Equal(payload.HubSpotEventId, result.HubSpotEventId);
        Assert.Equal(payload.CorrelationKey, result.CorrelationKey);
    }
}

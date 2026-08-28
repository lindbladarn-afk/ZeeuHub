using System.Text.Json;
using WebApp.Services.Integration.CustomerSync.Domain;

namespace WebApp.Services.Integration.CustomerSync.Background;

// Carries the minimum context needed to execute a customer sync job in the background worker.
public sealed class CustomerSyncBackgroundJobPayload
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Guid CompanyId { get; set; }
    public int JeevesCompanyCode { get; set; }
    public CustomerSyncDirection Direction { get; set; }
    public CustomerSyncTrigger Trigger { get; set; }
    public string? HubSpotEventId { get; set; }
    public string? HubSpotObjectId { get; set; }
    public string? CorrelationKey { get; set; }

    public string ToJson()
        => JsonSerializer.Serialize(this, JsonOptions);

    public static CustomerSyncBackgroundJobPayload FromJson(string payloadJson)
        => JsonSerializer.Deserialize<CustomerSyncBackgroundJobPayload>(payloadJson, JsonOptions)
           ?? throw new InvalidOperationException("CustomerSync background payload could not be deserialized.");
}

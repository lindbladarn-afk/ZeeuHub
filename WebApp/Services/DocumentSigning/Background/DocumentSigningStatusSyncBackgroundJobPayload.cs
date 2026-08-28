using System.Text.Json;

namespace WebApp.Services.DocumentSigning;

public sealed class DocumentSigningStatusSyncBackgroundJobPayload
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Guid SigningId { get; set; }

    public static string Serialize(DocumentSigningStatusSyncBackgroundJobPayload payload)
        => JsonSerializer.Serialize(payload, JsonOptions);

    public static DocumentSigningStatusSyncBackgroundJobPayload Deserialize(string payloadJson)
        => JsonSerializer.Deserialize<DocumentSigningStatusSyncBackgroundJobPayload>(payloadJson, JsonOptions)
           ?? throw new InvalidOperationException("Document signing status sync payload could not be deserialized.");
}

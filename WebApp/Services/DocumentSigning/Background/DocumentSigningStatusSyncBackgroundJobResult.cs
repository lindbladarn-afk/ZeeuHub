using System.Text.Json;
using WebApp.Models.DocumentSigning;

namespace WebApp.Services.DocumentSigning;

// Carries status-sync outcome data from the job handler to the generic presentation layer.
public sealed class DocumentSigningStatusSyncBackgroundJobResult
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool StatusChanged { get; set; }
    public Guid SigningId { get; set; }
    public long OrderNo { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string PortalStatus { get; set; } = string.Empty;
    public string ProviderStatus { get; set; } = string.Empty;
    public string SignerName { get; set; } = string.Empty;
    public string SignerEmail { get; set; } = string.Empty;
    public string MainFileName { get; set; } = string.Empty;
    public int AttachmentCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
    public bool SignedAndSealed { get; set; }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public DocumentSigningListItem ToListItem()
        => new()
        {
            Id = SigningId,
            OrderNo = OrderNo,
            DocumentTitle = DocumentTitle,
            DocumentId = DocumentId,
            PortalStatus = PortalStatus,
            ProviderStatus = ProviderStatus,
            SignerName = SignerName,
            SignerEmail = SignerEmail,
            MainFileName = MainFileName,
            AttachmentCount = AttachmentCount,
            CreatedAtUtc = CreatedAtUtc,
            StartedAtUtc = StartedAtUtc,
            CompletedAtUtc = CompletedAtUtc,
            LastSyncedAtUtc = LastSyncedAtUtc,
            SignedAndSealed = SignedAndSealed
        };

    public static DocumentSigningStatusSyncBackgroundJobResult FromListItem(DocumentSigningListItem item, bool statusChanged)
        => new()
        {
            StatusChanged = statusChanged,
            SigningId = item.Id,
            OrderNo = item.OrderNo,
            DocumentTitle = item.DocumentTitle,
            DocumentId = item.DocumentId,
            PortalStatus = item.PortalStatus,
            ProviderStatus = item.ProviderStatus,
            SignerName = item.SignerName,
            SignerEmail = item.SignerEmail,
            MainFileName = item.MainFileName,
            AttachmentCount = item.AttachmentCount,
            CreatedAtUtc = item.CreatedAtUtc,
            StartedAtUtc = item.StartedAtUtc,
            CompletedAtUtc = item.CompletedAtUtc,
            LastSyncedAtUtc = item.LastSyncedAtUtc,
            SignedAndSealed = item.SignedAndSealed
        };

    public static DocumentSigningStatusSyncBackgroundJobResult? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<DocumentSigningStatusSyncBackgroundJobResult>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}

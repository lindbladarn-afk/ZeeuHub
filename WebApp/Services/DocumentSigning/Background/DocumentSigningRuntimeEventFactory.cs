using WebApp.Models.Application;
using WebApp.Models.DocumentSigning;

namespace WebApp.Services.DocumentSigning;

internal static class DocumentSigningRuntimeEventFactory
{
    public static SidebarRuntimeEventRecord CreateSentEvent(
        long orderNo,
        Guid signingId,
        string documentTitle,
        string signerFirstName,
        string signerLastName)
    {
        return new SidebarRuntimeEventRecord
        {
            Source = "Oneflow",
            Title = "Signering skickad",
            Summary = $"{documentTitle} skickades till {signerFirstName} {signerLastName}.",
            LinkUrl = BuildDocumentSigningLink(orderNo, signingId),
            StatusLabel = "Sent",
            StatusTone = "info",
            IconClass = "fas fa-file-signature"
        };
    }

    public static SidebarRuntimeEventRecord CreateStatusChangedEvent(DocumentSigningListItem signing)
    {
        return new SidebarRuntimeEventRecord
        {
            Source = "Oneflow",
            Title = BuildEventTitle(signing),
            Summary = BuildEventSummary(signing),
            LinkUrl = BuildDocumentSigningLink(signing.OrderNo, signing.Id),
            StatusLabel = BuildEventStatusLabel(signing),
            StatusTone = BuildEventStatusTone(signing),
            IconClass = "fas fa-file-signature"
        };
    }

    private static string BuildDocumentSigningLink(long orderNo, Guid signingId)
    {
        return orderNo > 0
            ? $"/Integration/DocumentSigning?orderNo={orderNo}&selectedSigningId={signingId}"
            : $"/Integration/DocumentSigning?selectedSigningId={signingId}";
    }

    private static string BuildEventTitle(DocumentSigningListItem signing)
    {
        return signing.PortalStatus?.ToLowerInvariant() switch
        {
            "signed" => "Färdigsignerad",
            "waitinginternal" => "Mottagaren signerade",
            "rejected" => "Signering avvisad",
            "timedout" => "Signering förföll",
            "failed" => "Signering misslyckades",
            _ => "Signeringsstatus uppdaterad"
        };
    }

    private static string BuildEventSummary(DocumentSigningListItem signing)
    {
        return signing.PortalStatus?.ToLowerInvariant() switch
        {
            "signed" => $"{signing.DocumentTitle} är färdigsignerad.",
            "waitinginternal" => $"{signing.SignerName} har signerat. Väntar på ZeeU.",
            "rejected" => $"{signing.SignerName} avvisade {signing.DocumentTitle}.",
            "timedout" => $"{signing.DocumentTitle} passerade tidsgränsen.",
            "failed" => $"{signing.DocumentTitle} fick ett fel vid statussynk.",
            _ => $"{signing.DocumentTitle} uppdaterades i Oneflow."
        };
    }

    private static string BuildEventStatusLabel(DocumentSigningListItem signing)
    {
        return signing.PortalStatus?.ToLowerInvariant() switch
        {
            "signed" => "Signed",
            "waitinginternal" => "Partial",
            "rejected" => "Rejected",
            "timedout" => "Timed out",
            "failed" => "Failed",
            _ => "Updated"
        };
    }

    private static string BuildEventStatusTone(DocumentSigningListItem signing)
    {
        return signing.PortalStatus?.ToLowerInvariant() switch
        {
            "signed" => "success",
            "waitinginternal" => "info",
            "rejected" => "danger",
            "timedout" => "danger",
            "failed" => "danger",
            _ => "muted"
        };
    }
}

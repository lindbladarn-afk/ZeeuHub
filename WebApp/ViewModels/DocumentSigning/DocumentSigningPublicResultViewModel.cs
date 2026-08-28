namespace WebApp.ViewModels.DocumentSigning;

// View model for the public document signing result page.
public sealed class DocumentSigningPublicResultViewModel
{
    public string DocumentTitle { get; set; } = string.Empty;
    public string PortalStatus { get; set; } = string.Empty;
    public string ProviderStatus { get; set; } = string.Empty;
    public string SignerName { get; set; } = string.Empty;
    public string MainFileName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public bool SignedAndSealed { get; set; }
}

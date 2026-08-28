namespace WebApp.ViewModels.Admin;

// Holds the rendered verification mail shown in the admin preview modal.
public sealed class VerificationMailPreviewViewModel
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string Recipient { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string RedirectNotice { get; init; } = string.Empty;
    public string BodyHtml { get; init; } = string.Empty;
}

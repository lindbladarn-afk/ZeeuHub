namespace WebApp.Services.Integration.BankReconciliation.UploadFlow;

// Carries upload flow status feedback for TempData-backed redirects.
public sealed class BankReconciliationUploadFlowResult
{
    public string? UploadError { get; set; }
    public string? UploadInfo { get; set; }
    public string? StatusTone { get; set; }
    public string? StatusMessage { get; set; }
}

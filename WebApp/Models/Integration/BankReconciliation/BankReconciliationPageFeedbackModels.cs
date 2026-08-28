namespace WebApp.Models.Integration;

// Page feedback models keep TempData-derived status separate from controller plumbing.
public sealed class BankReconciliationPageFeedback
{
    public string? UploadError { get; set; }
    public string? UploadInfo { get; set; }
    public string? StatusMessage { get; set; }
    public string StatusTone { get; set; } = "info";
}

namespace WebApp.Services.NotifyMe;

// Compact execution result returned from shared scheduled and test-run paths.
internal sealed class PortalNotifyMeExecutionOutcome
{
    public string? Subject { get; set; }
    public DateTime? LoggedAt { get; set; }
    public bool LogCreated { get; set; }
    public bool MailQueued { get; set; }
    public string? MailStatus { get; set; }
}

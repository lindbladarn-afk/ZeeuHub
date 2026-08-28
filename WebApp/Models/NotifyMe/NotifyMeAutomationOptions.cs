namespace WebApp.Models.NotifyMe;

// Configuration model for the NotifyMe background automation worker.
public sealed class NotifyMeAutomationOptions
{
    public const string SectionName = "NotifyMe:Automation";

    public int PollIntervalMinutes { get; set; } = 5;

    public int BatchSize { get; set; } = 25;
}

namespace WebApp.Services.Integration.BankReconciliation.DemoSession;

// Carries user-facing status feedback from demo-session commands.
public sealed class BankReconciliationDemoSessionResult
{
    public string StatusTone { get; set; } = "info";
    public string? StatusMessage { get; set; }
}

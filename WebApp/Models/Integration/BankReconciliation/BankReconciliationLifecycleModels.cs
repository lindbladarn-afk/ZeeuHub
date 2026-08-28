// Defines close and reopen command results for bank reconciliation.
namespace WebApp.Models.Integration;

public sealed class BankReconciliationLifecycleCommandResult
{
    public bool Success { get; set; }
    public bool Conflict { get; set; }
    public int Version { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? ClosedByName { get; set; }
    public int ReviewCount { get; set; }
    public int UnmatchedCount { get; set; }
    public string? ErrorMessage { get; set; }
}

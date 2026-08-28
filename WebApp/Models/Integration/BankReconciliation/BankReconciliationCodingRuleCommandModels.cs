namespace WebApp.Models.Integration;

// Coding rule command models keep persistence results separate from MVC response handling.
public sealed class BankReconciliationCodingRuleCommandResult
{
    public bool Success { get; set; }
    public bool Conflict { get; set; }
    public string? ErrorMessage { get; set; }
    public int? Version { get; set; }
    public int? CurrentVersion { get; set; }
    public string? BankAccountKey { get; set; }
    public string? BankAccountLabel { get; set; }
    public List<BankReconciliationCodingRuleRow> Rows { get; set; } = new();
}

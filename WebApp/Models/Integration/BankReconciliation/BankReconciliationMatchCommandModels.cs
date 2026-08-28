namespace WebApp.Models.Integration;

// Match command models keep controller responses separate from reconciliation state persistence.
public class BankReconciliationMatchCommandResult
{
    public bool Success { get; set; }
    public bool Conflict { get; set; }
    public string? ErrorMessage { get; set; }
    public int? Version { get; set; }
    public int? CurrentVersion { get; set; }
    public int Count { get; set; }
    public BankReconciliationSavedMatch? Match { get; set; }
    public List<BankReconciliationSavedMatch> Matches { get; set; } = new();
    public List<BankReconciliationPaymentBundleSuggestion> PaymentBundleSuggestions { get; set; } = new();
}

namespace WebApp.Models.Integration;

// State query models describe the read-side bank reconciliation state shown in the UI.
public sealed class BankReconciliationStateQueryResult
{
    public bool Success { get; set; } = true;
    public int? Version { get; set; }
    public int MatchCount { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? ClosedByName { get; set; }
    public List<BankReconciliationStateMatchItem> Matches { get; set; } = new();
    public List<BankReconciliationStateActivityItem> RecentActivity { get; set; } = new();
}

public sealed class BankReconciliationStateMatchItem
{
    public string AllocationId { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string InvoiceId { get; set; } = string.Empty;
    public string MatchType { get; set; } = string.Empty;
    public string MatchRule { get; set; } = string.Empty;
    public decimal MatchedAmount { get; set; }
    public string Currency { get; set; } = "SEK";
}

public sealed class BankReconciliationStateActivityItem
{
    public DateTime CreatedAtUtc { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? TransactionId { get; set; }
    public string? InvoiceId { get; set; }
    public string? MatchType { get; set; }
    public string? MatchRule { get; set; }
    public decimal? MatchedAmount { get; set; }
    public string? Note { get; set; }
}

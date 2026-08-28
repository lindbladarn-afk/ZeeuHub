namespace WebApp.Models.Integration;

// Recommendation query models keep read-side bank reconciliation responses testable outside MVC.
public sealed class BankReconciliationRecommendationQueryResult
{
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public List<BankReconciliationRecommendationItem> Items { get; set; } = new();
}

public sealed class BankReconciliationAiSuggestionQueryResult
{
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public BankReconciliationAiSuggestionResult Result { get; set; } = new();
}

namespace WebApp.Services.Integration.BankReconciliation;

public sealed class BankReconciliationMatchingOptions
{
    public const string SectionName = "BankReconciliation:Matching";

    public decimal AmountTolerance { get; set; } = 1.0m;
    public int DateWindowDays { get; set; } = 7;
    public int RecommendationMinimumScore { get; set; } = 35;
    public int RecommendationMaxResults { get; set; } = 4;
    public int AutoMatchReferenceAmountScore { get; set; } = 90;
    public int AutoMatchReferenceScore { get; set; } = 70;
    public int AutoMatchAmountNameScore { get; set; } = 45;
    public int AutoMatchAmountDateScore { get; set; } = 45;
    public int ManualConfirmationMinimumScore { get; set; } = 80;
    public int MinimumExactReferenceLength { get; set; } = 4;
    public int MinimumPartialReferenceLength { get; set; } = 6;
}

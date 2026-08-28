namespace WebApp.Services.Integration.BankReconciliation;

// AI suggestion options keep bank reconciliation AI disabled unless explicitly configured.
public sealed class BankReconciliationAiSuggestionOptions
{
    public const string SectionName = "BankReconciliation:AiSuggestions";

    public bool Enabled { get; set; }
    public string PromptVersion { get; set; } = "bankrec-ai-suggestion-v1";
    public int MaxCandidates { get; set; } = 4;
    public decimal Temperature { get; set; } = 0.0m;
    public int MaxTokens { get; set; } = 700;
}

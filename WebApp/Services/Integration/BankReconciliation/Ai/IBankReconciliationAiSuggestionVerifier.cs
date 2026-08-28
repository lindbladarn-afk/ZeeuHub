using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation;

// Verifies AI suggestions against deterministic rule candidates before any result can be trusted.
public interface IBankReconciliationAiSuggestionVerifier
{
    BankReconciliationMatchEligibilityResult EvaluateEligibility(
        BankReconciliationAiSuggestionRequest request,
        BankReconciliationRecommendationItem ruleCandidate);

    BankReconciliationAiSuggestionVerificationResult Verify(
        BankReconciliationAiSuggestionRequest request,
        BankReconciliationAiSuggestionCandidate candidate);
}

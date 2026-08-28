using Microsoft.Extensions.Options;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation;

// Disabled AI suggestion service establishes the security contract before any external model is enabled.
public sealed class DisabledBankReconciliationAiSuggestionService : IBankReconciliationAiSuggestionService
{
    private readonly BankReconciliationAiSuggestionOptions _options;

    public DisabledBankReconciliationAiSuggestionService(IOptions<BankReconciliationAiSuggestionOptions>? options = null)
    {
        _options = options?.Value ?? new BankReconciliationAiSuggestionOptions();
    }

    public Task<BankReconciliationAiSuggestionResult> BuildSuggestionsAsync(
        BankReconciliationAiSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new BankReconciliationAiSuggestionResult
        {
            Enabled = false,
            Status = _options.Enabled ? "provider-not-configured" : "disabled",
            PromptVersion = _options.PromptVersion,
            InputHash = BankReconciliationAiSuggestionInputHasher.BuildInputHash(request),
            Message = "AI-förslag är avstängt. Regelmotorns kandidater används tills en godkänd AI-provider är konfigurerad.",
            Suggestions = new List<BankReconciliationAiSuggestionCandidate>()
        });
    }
}

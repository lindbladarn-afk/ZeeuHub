// Defines semantic metric and dimension resolution for Intelligence query plans.
using WebApp.Models.AI;

namespace WebApp.Services.Application.AI;

public interface IAiSemanticCatalog
{
    string BuildPromptContext();
    AiQueryPlan CreateFallbackPlan(string question);
    AiQueryPlanValidation ValidateAndNormalize(AiQueryPlan? plan, string question);
    string? GetMetricLabel(string? metricKey);
}

public sealed record AiQueryPlanValidation(
    bool Success,
    AiQueryPlan Plan,
    string? Error = null);

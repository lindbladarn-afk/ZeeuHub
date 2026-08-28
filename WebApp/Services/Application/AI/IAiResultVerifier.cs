// Defines deterministic evidence checks for generated Intelligence answers.
using WebApp.Models.AI;

namespace WebApp.Services.Application.AI;

public interface IAiResultVerifier
{
    AiQueryEvidence Verify(
        string answer,
        SqlQueryResult query,
        AiQueryPlan? plan,
        string dataSource,
        string? metricLabel,
        string sql);
}

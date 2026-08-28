// Defines the selected-database boundary used immediately before read-only execution.
namespace WebApp.Services.Application.AI;

public interface IAiSqlSecurityPolicy
{
    AiSqlPolicyResult Validate(string sql);
}

public sealed record AiSqlPolicyResult(
    bool Success,
    string Sql,
    string? ErrorCode = null,
    string? Error = null);

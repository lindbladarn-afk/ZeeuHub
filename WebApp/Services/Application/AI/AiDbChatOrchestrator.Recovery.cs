// Repairs model-generated SQL once before deterministic recovery or a user-facing error is returned.
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WebApp.Models.AI;

namespace WebApp.Services.Application.AI;

public sealed partial class AiDbChatOrchestrator
{
    private async Task<SqlRecoveryResult?> TryRepairAndExecuteSqlAsync(
        string question,
        string? schemaText,
        int? companyCode,
        string failedSql,
        string failureReason,
        string connectionString,
        string memoryKey,
        AiProgressCallback? progress,
        AiExecutionDiagnostics diagnostics,
        TokenUsageTotals tokenUsage,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(schemaText) ||
            string.IsNullOrWhiteSpace(failedSql) ||
            string.IsNullOrWhiteSpace(failureReason))
        {
            return null;
        }

        await ReportProgressAsync(
            progress,
            "repairing",
            "Reparerar databasfrågan automatiskt",
            80,
            ct);
        var planningTimer = Stopwatch.StartNew();
        var repairedDraft = await GenerateSqlRepairAsync(
            question,
            schemaText,
            companyCode,
            failedSql,
            failureReason,
            memoryKey,
            tokenUsage,
            ct);
        diagnostics.PlanningDurationMs += planningTimer.ElapsedMilliseconds;

        if (repairedDraft.RequiresClarification ||
            string.IsNullOrWhiteSpace(repairedDraft.Sql) ||
            IsSameSql(repairedDraft.Sql, failedSql))
        {
            return null;
        }

        var repairedSql = SanitizeGeneratedSql(repairedDraft.Sql, question);
        var policy = _sqlSecurityPolicy.Validate(repairedSql);
        if (!policy.Success)
            return null;

        await ReportProgressAsync(
            progress,
            "querying",
            "Kör den reparerade databasfrågan",
            84,
            ct);
        var queryTimer = Stopwatch.StartNew();
        var query = await _sql.ExecuteSelectAsync(connectionString, policy.Sql, maxRows: 200, ct: ct);
        diagnostics.SqlDurationMs += queryTimer.ElapsedMilliseconds;
        if (!query.Success)
            return null;

        return new SqlRecoveryResult(
            query,
            query.ExecutedSql ?? policy.Sql,
            repairedDraft.Plan);
    }

    private static string SanitizeGeneratedSql(string sql, string question)
    {
        var sanitized = AiSqlSyntaxSanitizer.FixSqlServerTopSyntax(sql);
        sanitized = AiSqlSyntaxSanitizer.FixSqlAggregateCasts(sanitized);
        sanitized = AiSqlSyntaxSanitizer.NormalizeBiPkJoins(sanitized);

        var topN = ExtractTopNFromQuestion(question);
        if (LooksLikeTopCustomersQuestion(question) && !HasExplicitTopNumber(question))
        {
            topN = 3;
        }

        if (topN is not null)
        {
            sanitized = AiSqlSyntaxSanitizer.NormalizeTopNForQuestion(sanitized, topN.Value);
        }

        return AiSqlSyntaxSanitizer.FixDanglingTopParentheses(sanitized);
    }

    private sealed record SqlRecoveryResult(
        SqlQueryResult Query,
        string Sql,
        AiQueryPlan? Plan);
}

// Contains deterministic recovery paths used only after the AI-led SQL flow cannot produce a usable result.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WebApp.Models.AI;

namespace WebApp.Services.Application.AI;

public sealed partial class AiDbChatOrchestrator
{
    private async Task<AiQueryResponse?> TryExecuteDeterministicFallbackAsync(
        string resolvedQuestion,
        string originalQuestion,
        string? schemaText,
        string connectionString,
        AiDataSourceInfo dataSource,
        string memoryKey,
        bool includeSqlInResponse,
        bool suppressCustomerFollowUp,
        string? excludedSql,
        AiProgressCallback? progress,
        AiExecutionDiagnostics diagnostics,
        TokenUsageTotals tokenUsage,
        CancellationToken ct)
    {
        var entityResponse = await TryExecuteEntityListFallbackAsync(
            resolvedQuestion,
            originalQuestion,
            schemaText,
            connectionString,
            dataSource,
            memoryKey,
            includeSqlInResponse,
            excludedSql,
            progress,
            diagnostics,
            ct);
        if (entityResponse is not null)
            return entityResponse;

        if (LooksLikeCustomerAggregateQuestion(resolvedQuestion))
        {
            return await TryExecuteRankingFallbackAsync(
                BuildTopCustomersFallbackSqlCandidates(resolvedQuestion, schemaText),
                "Jag hittade inga kunder som matchar den valda perioden eller filtreringen.",
                "Resultatet är kontrollerat mot den valda datakällan.",
                resolvedQuestion,
                originalQuestion,
                connectionString,
                dataSource,
                memoryKey,
                includeSqlInResponse,
                isCustomerRanking: true,
                suppressCustomerFollowUp,
                excludedSql,
                progress,
                diagnostics,
                tokenUsage,
                ct);
        }

        if (LooksLikeMonthlyRevenueQuestion(resolvedQuestion) &&
            !LooksLikeYearToDateRevenueComparisonQuestion(resolvedQuestion))
        {
            return await TryExecuteRankingFallbackAsync(
                BuildMonthlyRevenueFallbackSqlCandidates(resolvedQuestion, schemaText),
                "Jag hittade ingen omsättning som matchar den valda perioden.",
                "Resultatet är kontrollerat mot den valda datakällan.",
                resolvedQuestion,
                originalQuestion,
                connectionString,
                dataSource,
                memoryKey,
                includeSqlInResponse,
                isCustomerRanking: false,
                suppressCustomerFollowUp,
                excludedSql,
                progress,
                diagnostics,
                tokenUsage,
                ct);
        }

        if (LooksLikeCurrentYearRevenueQuestion(resolvedQuestion))
        {
            return await TryExecuteRankingFallbackAsync(
                BuildYearToDateRevenueSqlCandidates(resolvedQuestion, schemaText),
                "Jag hittade ingen omsättning för innevarande år hittills.",
                "Resultatet är kontrollerat från årets början till dagens datum.",
                resolvedQuestion,
                originalQuestion,
                connectionString,
                dataSource,
                memoryKey,
                includeSqlInResponse,
                isCustomerRanking: false,
                suppressCustomerFollowUp,
                excludedSql,
                progress,
                diagnostics,
                tokenUsage,
                ct);
        }

        if (LooksLikeYearToDateRevenueComparisonQuestion(resolvedQuestion))
        {
            return await TryExecuteRankingFallbackAsync(
                BuildYearToDateRevenueComparisonSqlCandidates(resolvedQuestion, schemaText),
                "Jag hittade ingen omsättning för de jämförda perioderna.",
                "Resultatet är kontrollerat mot samma datumintervall i båda åren.",
                resolvedQuestion, originalQuestion, connectionString, dataSource, memoryKey,
                includeSqlInResponse, isCustomerRanking: false, suppressCustomerFollowUp,
                excludedSql, progress, diagnostics, tokenUsage, ct);
        }

        if (LooksLikeTopProductsQuestion(resolvedQuestion))
        {
            return await TryExecuteRankingFallbackAsync(
                BuildTopProductsFallbackSqlCandidates(resolvedQuestion, schemaText),
                "Jag hittade inga produkter som matchar den valda perioden eller filtreringen.",
                "Resultatet är kontrollerat mot den valda datakällan.",
                resolvedQuestion,
                originalQuestion,
                connectionString,
                dataSource,
                memoryKey,
                includeSqlInResponse,
                isCustomerRanking: false,
                suppressCustomerFollowUp,
                excludedSql,
                progress,
                diagnostics,
                tokenUsage,
                ct);
        }

        return null;
    }

    private async Task<AiQueryResponse?> TryExecuteEntityListFallbackAsync(
        string resolvedQuestion,
        string originalQuestion,
        string? schemaText,
        string connectionString,
        AiDataSourceInfo dataSource,
        string memoryKey,
        bool includeSqlInResponse,
        string? excludedSql,
        AiProgressCallback? progress,
        AiExecutionDiagnostics diagnostics,
        CancellationToken ct)
    {
        var fallback = BuildSimpleEntityListFallback(resolvedQuestion, schemaText);
        if (fallback is null || IsSameSql(fallback.Sql, excludedSql))
            return null;

        var policy = _sqlSecurityPolicy.Validate(fallback.Sql);
        if (!policy.Success)
            return null;

        await ReportProgressAsync(
            progress,
            "recovering",
            $"Återhämtar analysen och hämtar {fallback.PluralLabel}",
            82,
            ct);
        var queryTimer = Stopwatch.StartNew();
        var query = await _sql.ExecuteSelectAsync(connectionString, policy.Sql, maxRows: 200, ct: ct);
        diagnostics.SqlDurationMs += queryTimer.ElapsedMilliseconds;
        if (!query.Success)
            return null;

        var answer = query.RowCount == 0
            ? $"Jag hittade inga {fallback.PluralLabel} i den valda databasen."
            : $"Här är {fallback.PluralLabel} från den valda databasen.";
        _memory.AppendTurn(memoryKey, originalQuestion, answer);

        var plan = _semanticCatalog.CreateFallbackPlan(resolvedQuestion);
        RememberDatabaseResult(memoryKey, query, plan);
        var evidence = _resultVerifier.Verify(
            answer,
            query,
            plan,
            dataSource.Name,
            fallback.MetricLabel,
            policy.Sql);

        return new AiQueryResponse
        {
            Answer = answer,
            Sql = includeSqlInResponse ? (query.ExecutedSql ?? policy.Sql) : "",
            Warning = query.Truncated
                ? $"Analysen återhämtades automatiskt. Visar de första 200 {fallback.PluralLabel}."
                : "Analysen återhämtades automatiskt med en verifierad reservfråga.",
            Columns = query.Columns,
            Rows = query.Rows,
            RowCount = query.RowCount,
            Truncated = query.Truncated,
            Plan = plan,
            Evidence = evidence
        };
    }

    private async Task<AiQueryResponse?> TryExecuteRankingFallbackAsync(
        IReadOnlyList<string> sqlCandidates,
        string emptyAnswer,
        string recoveryWarning,
        string resolvedQuestion,
        string originalQuestion,
        string connectionString,
        AiDataSourceInfo dataSource,
        string memoryKey,
        bool includeSqlInResponse,
        bool isCustomerRanking,
        bool suppressCustomerFollowUp,
        string? excludedSql,
        AiProgressCallback? progress,
        AiExecutionDiagnostics diagnostics,
        TokenUsageTotals tokenUsage,
        CancellationToken ct)
    {
        SqlQueryResult? emptyQuery = null;
        string? emptySql = null;
        var plan = _semanticCatalog.CreateFallbackPlan(resolvedQuestion);
        if (isCustomerRanking && string.Equals(plan.Metric, "custom", StringComparison.OrdinalIgnoreCase))
        {
            plan.Metric = Regex.IsMatch(resolvedQuestion, @"(?is)\b(beställ\w*|order\w*)\b")
                ? "order_value"
                : "net_revenue";
        }

        foreach (var candidate in sqlCandidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || IsSameSql(candidate, excludedSql))
                continue;

            var policy = _sqlSecurityPolicy.Validate(candidate);
            if (!policy.Success)
                continue;

            await ReportProgressAsync(
                progress,
                "recovering",
                "Återhämtar analysen med en verifierad reservfråga",
                82,
                ct);
            var queryTimer = Stopwatch.StartNew();
            var query = await _sql.ExecuteSelectAsync(connectionString, policy.Sql, maxRows: 200, ct: ct);
            diagnostics.SqlDurationMs += queryTimer.ElapsedMilliseconds;
            if (!query.Success)
                continue;

            if (query.RowCount == 0)
            {
                emptyQuery ??= query;
                emptySql ??= policy.Sql;
                continue;
            }

            await ReportProgressAsync(progress, "summarizing", "Sammanfattar det återhämtade resultatet", 90, ct);
            var summaryTimer = Stopwatch.StartNew();
            var answer = await SummarizeAsync(resolvedQuestion, query, dataSource, tokenUsage, ct);
            diagnostics.SummaryDurationMs += summaryTimer.ElapsedMilliseconds;
            answer = BuildDeterministicYearToDateComparisonAnswer(query) ?? answer;
            if (isCustomerRanking)
            {
                answer = AppendCustomerFollowUpIfNeeded(answer, resolvedQuestion, suppressCustomerFollowUp);
            }

            _memory.AppendTurn(memoryKey, originalQuestion, answer);
            RememberDatabaseResult(memoryKey, query, plan);
            var evidence = _resultVerifier.Verify(
                answer,
                query,
                plan,
                dataSource.Name,
                _semanticCatalog.GetMetricLabel(plan.Metric),
                policy.Sql);

            return new AiQueryResponse
            {
                Answer = answer,
                Sql = includeSqlInResponse ? (query.ExecutedSql ?? policy.Sql) : "",
                Warning = BuildRankingResultWarning(recoveryWarning, resolvedQuestion, query),
                Columns = query.Columns,
                Rows = query.Rows,
                RowCount = query.RowCount,
                Truncated = query.Truncated,
                Plan = plan,
                Evidence = evidence
            };
        }

        return emptyQuery is null || string.IsNullOrWhiteSpace(emptySql)
            ? null
            : BuildNoDataRankingResponse(
                emptyAnswer,
                originalQuestion,
                memoryKey,
                emptyQuery,
                plan,
                dataSource,
                emptySql,
                includeSqlInResponse);
    }

    private static string BuildRankingResultWarning(
        string recoveryWarning,
        string question,
        SqlQueryResult query)
    {
        var requestedLimit = ExtractTopNFromQuestion(question);
        if (!requestedLimit.HasValue || query.Truncated || query.RowCount >= requestedLimit.Value)
            return recoveryWarning;

        return $"{recoveryWarning} Visar {query.RowCount} av efterfrågade {requestedLimit.Value}; " +
               "övriga kunder matchade inte den valda perioden eller filtreringen.";
    }

    private static string? BuildDeterministicYearToDateComparisonAnswer(SqlQueryResult query)
    {
        var currentIndex = FindColumnIndex(query.Columns, "CurrentYearToDate", "CurrentPeriod");
        var previousIndex = FindColumnIndex(query.Columns, "PreviousYearToDate", "PreviousPeriod");
        var differenceIndex = FindColumnIndex(query.Columns, "Difference", "Variance", "Delta");
        if (query.Rows.Count != 1 || currentIndex < 0 || previousIndex < 0 || differenceIndex < 0)
            return null;

        var row = query.Rows[0];
        if (currentIndex >= row.Count || previousIndex >= row.Count || differenceIndex >= row.Count)
            return null;

        try
        {
            var current = Convert.ToDecimal(row[currentIndex], CultureInfo.InvariantCulture);
            var previous = Convert.ToDecimal(row[previousIndex], CultureInfo.InvariantCulture);
            var difference = Convert.ToDecimal(row[differenceIndex], CultureInfo.InvariantCulture);
            var formatter = CultureInfo.GetCultureInfo("sv-SE");
            var amount = Math.Abs(difference).ToString("N2", formatter);
            var direction = difference > 0 ? "högre" : difference < 0 ? "lägre" : "oförändrad";
            return difference == 0
                ? $"Omsättningen är oförändrad jämfört med samma period förra året ({current.ToString("N2", formatter)} kr)."
                : $"Omsättningen är {direction} i år än samma period förra året med {amount} kr. " +
                  $"I år: {current.ToString("N2", formatter)} kr. Förra året: {previous.ToString("N2", formatter)} kr.";
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static int FindColumnIndex(IReadOnlyList<string> columns, params string[] aliases)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (aliases.Any(alias =>
                    columns[index].Equals(alias, StringComparison.OrdinalIgnoreCase)))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsSameSql(string left, string? right)
    {
        return !string.IsNullOrWhiteSpace(right) &&
               string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

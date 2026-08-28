using System.Linq;
using System.Text.RegularExpressions;

namespace WebApp.Services.Application.AI;

/// <summary>
/// Normalizes and repairs common SQL syntax issues from LLM output.
/// This class controls safe, deterministic SQL text rewrites before execution
/// (for example TOP placement and known malformed TOP parenthesis variants).
/// </summary>
public static class AiSqlSyntaxSanitizer
{
    public static string FixSqlServerTopSyntax(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return sql;

        // Only attempt this for SQL Server-like queries.
        if (!Regex.IsMatch(sql, @"(?is)\btop\b"))
            return sql;

        // If it's already correct in the outermost SELECT, leave it.
        if (Regex.IsMatch(sql, @"(?is)^\s*(with\b.+?\)\s*)*select\s+(distinct\s+)?top\s*\(?\s*\d+\s*\)?\b"))
            return sql;

        var result = sql;

        // Case 1: "... ORDER BY <expr> TOP N" (TOP placed after ORDER BY -> invalid in SQL Server).
        var orderTop = Regex.Match(result, @"(?is)\border\s+by\b(?<order>.+?)\s+top\s*\(?\s*(?<n>\d+)\s*\)?\s*;?\s*$");
        if (orderTop.Success)
        {
            var n = orderTop.Groups["n"].Value;

            // Remove trailing "TOP N"
            result = Regex.Replace(result, @"(?is)\s+top\s*\(?\s*\d+\s*\)?\s*;?\s*$", "");

            // Insert TOP after the last SELECT before the ORDER BY
            var orderByIdx = Regex.Match(result, @"(?is)\border\s+by\b").Index;
            var selects = Regex.Matches(result, @"(?is)\bselect\b");
            var targetSelect = selects.Cast<Match>().LastOrDefault(m => m.Index < orderByIdx);

            if (targetSelect is not null)
            {
                var afterSelect = targetSelect.Index + targetSelect.Length;
                var tail = result.Substring(afterSelect);
                var distinctMatch = Regex.Match(tail, @"(?is)^\s*distinct\b\s*");
                var insertAt = afterSelect + distinctMatch.Length;

                // Avoid inserting if this SELECT already has TOP.
                if (!Regex.IsMatch(tail, @"(?is)^\s*(distinct\s+)?top\b"))
                    result = result.Insert(insertAt, $" TOP ({n}) ");
            }

            return result;
        }

        // Case 2: "SELECT <cols> TOP N FROM ..." (TOP placed after column list -> invalid).
        // Try to fix on the first SELECT that looks like an outer query.
        var outerSelect = Regex.Match(result, @"(?is)\bselect\b");
        if (outerSelect.Success)
        {
            var selectIdx = outerSelect.Index;
            var selectTail = result.Substring(selectIdx);
            var m = Regex.Match(selectTail, @"(?is)^\s*select\s+(?<distinct>distinct\s+)?(?<cols>.+?)\s+top\s*\(?\s*(?<n>\d+)\s*\)?\s+(?<rest>from\s+.+)$");
            if (m.Success)
            {
                var distinct = m.Groups["distinct"].Value;
                var cols = m.Groups["cols"].Value.Trim();
                var n = m.Groups["n"].Value;
                var rest = m.Groups["rest"].Value;

                var fixedTail = $"SELECT {distinct}TOP ({n}) {cols} {rest}";
                return result.Substring(0, selectIdx) + fixedTail;
            }
        }

        return result;
    }

    public static string FixSqlAggregateCasts(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return sql;

        var result = sql;

        result = Regex.Replace(result,
            @"(?is)\bsum\s*\(\s*(?<expr>(?<alias>\w+\.)?(?<col>linetotal|salesamount|totalamount|revenue|amount|belopp|omsatt|intakt))\s*\)",
            m =>
            {
                var expr = m.Groups["expr"].Value;
                return $"SUM(CAST({expr} AS decimal(18,2)))";
            });

        result = Regex.Replace(result,
            @"(?is)\bsum\s*\(\s*(?<expr>(?<alias>\w+\.)?(?<col>orderqty|quantity|qty|units|antal))\s*\)",
            m =>
            {
                var expr = m.Groups["expr"].Value;
                return $"SUM(CAST({expr} AS int))";
            });

        return result;
    }

    public static string NormalizeBiPkJoins(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return sql;

        var result = sql;
        const string id =
            @"(?:\[[^\]]+\]|\w+)(?:\s*\.\s*(?:\[[^\]]+\]|\w+)){0,2}";

        // q_zu_bi_fsg <-> q_zu_bi_item: prefer AR_PK join over (Company + Item No).
        // Pattern 1: alias.[Company] = alias.[Company] AND alias.[Item No] = alias.[Item No]
        result = Regex.Replace(
            result,
            $@"(?is)(?<a>{id})\s*\.\s*(?:\[Company\]|Company)\s*=\s*(?<b>{id})\s*\.\s*(?:\[Company\]|Company)\s*AND\s*\k<a>\s*\.\s*(?:\[Item\s*No\]|\[ItemNo\]|ItemNo|Item\s+No)\s*=\s*\k<b>\s*\.\s*(?:\[Item\s*No\]|\[ItemNo\]|ItemNo|Item\s+No)",
            "${a}.[AR_PK] = ${b}.[AR_PK]");

        // Pattern 2: alias.[Item No] = alias.[Item No] AND alias.[Company] = alias.[Company]
        result = Regex.Replace(
            result,
            $@"(?is)(?<a>{id})\s*\.\s*(?:\[Item\s*No\]|\[ItemNo\]|ItemNo|Item\s+No)\s*=\s*(?<b>{id})\s*\.\s*(?:\[Item\s*No\]|\[ItemNo\]|ItemNo|Item\s+No)\s*AND\s*\k<a>\s*\.\s*(?:\[Company\]|Company)\s*=\s*\k<b>\s*\.\s*(?:\[Company\]|Company)",
            "${a}.[AR_PK] = ${b}.[AR_PK]");

        return result;
    }

    public static string NormalizeTopNForQuestion(string sql, int topN)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return sql;

        topN = int.Clamp(topN, 1, 200);

        if (Regex.IsMatch(sql, @"(?is)\boffset\b"))
        {
            var withoutTop = Regex.Replace(sql, @"(?is)\btop\s*\(?\s*\d+\s*\)?\b", "");

            if (Regex.IsMatch(withoutTop, @"(?is)\bfetch\s+next\b"))
            {
                return Regex.Replace(withoutTop, @"(?is)\bfetch\s+next\s+\d+\s+rows\s+only",
                    $"FETCH NEXT {topN} ROWS ONLY");
            }

            var offsetTail = Regex.Match(withoutTop, @"(?is)\boffset\s+(?<n>\d+)\s+rows\s*;?\s*$");
            if (offsetTail.Success)
            {
                var offset = offsetTail.Groups["n"].Value;
                return Regex.Replace(withoutTop, @"(?is)\boffset\s+\d+\s+rows\s*;?\s*$",
                    $"OFFSET {offset} ROWS FETCH NEXT {topN} ROWS ONLY");
            }

            return withoutTop;
        }

        return ForceSqlServerTopAtSelect(sql, topN);
    }

    public static string ForceSqlServerTopAtSelect(string sql, int topN)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return sql;

        topN = int.Clamp(topN, 1, 200);

        // Strip any TOP occurrences (we'll re-inject a safe TOP after SELECT).
        var stripped = Regex.Replace(sql, @"(?is)\btop\s*\(?\s*\d+\s*\)?\b", "");

        // If query uses OFFSET/FETCH, don't inject TOP (can conflict), just return stripped.
        if (Regex.IsMatch(stripped, @"(?is)\boffset\b.+\bfetch\b"))
            return stripped;

        // Insert TOP after the first SELECT (outer query). Also handle DISTINCT.
        var m = Regex.Match(stripped, @"(?is)^\s*(with\b.+?\)\s*)*(?<sel>select)\s+(?<distinct>distinct\s+)?");
        if (!m.Success)
            return stripped;

        var insertAt = m.Index + m.Length;
        return stripped.Insert(insertAt, $"TOP ({topN}) ");
    }

    public static string FixDanglingTopParentheses(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return sql;

        var result = sql;

        // Fix common malformed pattern from LLM: "TOP (1) )" -> "TOP (1)"
        result = Regex.Replace(
            result,
            @"(?is)\btop\s*\(\s*(?<n>\d+)\s*\)\s*\)",
            m => $"TOP ({m.Groups["n"].Value})");

        // Also handle "TOP 1 )" -> "TOP (1)"
        result = Regex.Replace(
            result,
            @"(?is)\btop\s+(?<n>\d+)\s*\)",
            m => $"TOP ({m.Groups["n"].Value})");

        return result;
    }
}


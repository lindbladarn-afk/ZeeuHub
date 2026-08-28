// Builds traceable evidence and verifies numeric answer claims against SQL result cells.
using System.Globalization;
using System.Text.RegularExpressions;
using WebApp.Models.AI;

namespace WebApp.Services.Application.AI;

public sealed class AiResultVerifier : IAiResultVerifier
{
    private static readonly Regex NumberPattern = new(
        @"(?<![\p{L}\d])[-+]?\d{1,3}(?:[ .]\d{3})*(?:[,.]\d+)?|(?<![\p{L}\d])[-+]?\d+(?:[,.]\d+)?",
        RegexOptions.Compiled);

    private static readonly Regex SourceTablePattern = new(
        @"(?is)\b(?:from|join)\s+(?<table>(?:\[[^\]]+\]|[a-zA-Z0-9_]+)\s*\.\s*(?:\[[^\]]+\]|[a-zA-Z0-9_]+))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public AiQueryEvidence Verify(
        string answer,
        SqlQueryResult query,
        AiQueryPlan? plan,
        string dataSource,
        string? metricLabel,
        string sql)
    {
        var resultNumbers = query.Rows
            .SelectMany(row => row)
            .Select(TryNormalizeNumber)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToList();

        var claims = NumberPattern.Matches(answer ?? string.Empty)
            .Select(match => TryParseNumber(match.Value))
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .Where(value => value < 1900 || value > 2100)
            .ToList();

        var unverifiedClaims = claims
            .Where(claim => !resultNumbers.Any(value => ApproximatelyEqual(value, claim)))
            .ToList();

        var evidence = new AiQueryEvidence
        {
            VerificationStatus = unverifiedClaims.Count == 0 ? "verified" : "needs_review",
            DataSource = dataSource,
            MetricLabel = metricLabel,
            Period = plan?.Period,
            Dimensions = plan?.Dimensions?.Take(4).ToList() ?? [],
            SourceTables = SourceTablePattern.Matches(sql ?? string.Empty)
                .Select(match => Regex.Replace(match.Groups["table"].Value, @"[\[\]\s]", string.Empty))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList(),
            Facts = BuildFacts(query)
        };

        if (query.Truncated)
            evidence.Notes.Add("Resultatet är begränsat och visar inte alla matchande rader.");
        if (unverifiedClaims.Count > 0)
            evidence.Notes.Add("En eller flera siffror i textsammanfattningen kunde inte verifieras mot resultatraderna.");

        return evidence;
    }

    private static List<string> BuildFacts(SqlQueryResult query)
    {
        if (query.Rows.Count == 0)
            return [];

        var firstRow = query.Rows[0];
        return query.Columns
            .Select((column, index) => new { column, index })
            .Where(item => item.index < firstRow.Count)
            .Take(4)
            .Select(item => $"{item.column}: {FormatValue(firstRow[item.index])}")
            .ToList();
    }

    private static string FormatValue(object? value) =>
        value switch
        {
            null => "–",
            DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            decimal number => number.ToString("0.##", CultureInfo.GetCultureInfo("sv-SE")),
            double number => number.ToString("0.##", CultureInfo.GetCultureInfo("sv-SE")),
            _ => value.ToString() ?? "–"
        };

    private static decimal? TryNormalizeNumber(object? value)
    {
        if (value is null)
            return null;
        if (value is byte or short or int or long or float or double or decimal)
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        return null;
    }

    private static decimal? TryParseNumber(string value)
    {
        var normalized = value.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (normalized.Contains(',') && normalized.Contains('.'))
            normalized = normalized.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        else
            normalized = normalized.Replace(',', '.');

        return decimal.TryParse(
            normalized,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    private static bool ApproximatelyEqual(decimal left, decimal right)
    {
        var tolerance = Math.Max(0.01m, Math.Abs(right) * 0.0001m);
        return Math.Abs(left - right) <= tolerance;
    }
}

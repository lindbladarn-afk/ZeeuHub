// Owns the versioned business vocabulary used to normalize Intelligence plans.
using System.Text;
using System.Text.RegularExpressions;
using WebApp.Models.AI;

namespace WebApp.Services.Application.AI;

public sealed class AiSemanticCatalog : IAiSemanticCatalog
{
    private const string CustomMetric = "custom";
    private static readonly HashSet<string> AllowedIntents = new(StringComparer.OrdinalIgnoreCase)
    {
        "lookup", "aggregate", "ranking", "trend", "comparison", "detail"
    };
    private static readonly HashSet<string> AllowedComparisons = new(StringComparer.OrdinalIgnoreCase)
    {
        "none", "current_vs_previous_same_period", "period_over_period", "actual_vs_budget"
    };
    private static readonly HashSet<string> AllowedTimeGrains = new(StringComparer.OrdinalIgnoreCase)
    {
        "day", "week", "month", "quarter", "year"
    };
    private static readonly HashSet<string> AllowedResultRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "metric", "dimension", "time", "current_period", "previous_period", "difference"
    };

    private static readonly IReadOnlyList<MetricDefinition> Metrics =
    [
        new("net_revenue", "Omsättning", "SEK", "sum",
            ["omsättning", "omsatt", "intäkt", "revenue", "sales", "försäljning"],
            ["BestValue", "LineTotal", "SalesAmount", "Revenue", "Amount", "Belopp"]),
        new("quantity_sold", "Sålt antal", "antal", "sum",
            ["antal", "sålda", "sålt", "enheter", "quantity", "qty"],
            ["OrderQty", "Quantity", "Qty", "Units", "Antal"]),
        new("invoice_balance", "Utestående fakturabelopp", "SEK", "sum",
            ["utestående", "obetald", "obetalda", "förfallen", "förfallna", "invoice balance"],
            ["RemainingAmount", "OpenAmount", "Balance", "Restbelopp"]),
        new("order_value", "Ordervärde", "SEK", "sum",
            ["ordervärde", "order value", "orderingång", "orderingang"],
            ["OrderValue", "LineTotal", "Amount", "Belopp"]),
        new("record_count", "Antal poster", "antal", "count",
            ["hur många", "antal kunder", "antal order", "antal fakturor", "count"],
            ["Id", "PK", "RowId"]),
        new(CustomMetric, "Anpassat mått", null, null, [], [])
    ];

    private static readonly IReadOnlyList<DimensionDefinition> Dimensions =
    [
        new("customer", "Kund", ["kund", "kunder", "customer"]),
        new("product", "Artikel", ["artikel", "artiklar", "produkt", "produkter", "item", "product"]),
        new("month", "Månad", ["månadsvis", "per månad", "by month"]),
        new("year", "År", ["per år", "årsvis", "yearly"]),
        new("salesperson", "Säljare", ["säljare", "salesperson"]),
        new("supplier", "Leverantör", ["leverantör", "supplier"]),
        new("project", "Projekt", ["projekt", "project"])
    ];

    public string BuildPromptContext()
    {
        var text = new StringBuilder();
        text.AppendLine("SEMANTIC CATALOG v1:");
        text.AppendLine("Use only these metric and dimension keys in plan. Use metric=custom only when no listed metric fits.");
        foreach (var metric in Metrics)
        {
            text.Append("- metric ").Append(metric.Key).Append(": ").Append(metric.Label);
            if (!string.IsNullOrWhiteSpace(metric.Unit))
                text.Append(" [").Append(metric.Unit).Append(']');
            if (metric.ColumnHints.Count > 0)
                text.Append("; column hints: ").Append(string.Join(", ", metric.ColumnHints));
            text.AppendLine();
        }

        foreach (var dimension in Dimensions)
            text.AppendLine($"- dimension {dimension.Key}: {dimension.Label}");

        return text.ToString().Trim();
    }

    public AiQueryPlan CreateFallbackPlan(string question)
    {
        var normalized = Normalize(question);
        var metric = ResolveMetric(normalized);
        var dimensions = Dimensions
            .Where(d => d.Synonyms.Any(normalized.Contains))
            .Select(d => d.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var comparison = ResolveComparison(normalized);
        var intent = comparison is not null
            ? "comparison"
            : Regex.IsMatch(normalized, @"\b(top|topp|störst|bäst|mest|högst)\b")
            ? "ranking"
            : dimensions.Contains("month", StringComparer.OrdinalIgnoreCase) ||
              dimensions.Contains("year", StringComparer.OrdinalIgnoreCase)
                ? "trend"
                : "aggregate";

        var limitMatch = Regex.Match(normalized, @"\b(?:top|topp|visa)\s+(?<limit>\d{1,3})\b");
        int? limit = limitMatch.Success && int.TryParse(limitMatch.Groups["limit"].Value, out var parsed)
            ? Math.Clamp(parsed, 1, 200)
            : intent == "ranking" ? 5 : null;

        return new AiQueryPlan
        {
            Intent = intent,
            Metric = metric.Key,
            Dimensions = dimensions,
            Period = ResolvePeriod(normalized),
            Comparison = comparison,
            TimeGrain = dimensions.Contains("month", StringComparer.OrdinalIgnoreCase) ? "month" : null,
            ResultContract = BuildDefaultResultContract(intent, comparison, dimensions),
            Sort = intent == "ranking" ? "descending" : null,
            Limit = limit
        };
    }

    public AiQueryPlanValidation ValidateAndNormalize(AiQueryPlan? plan, string question)
    {
        plan ??= CreateFallbackPlan(question);
        var requestedIntent = plan.Intent ?? string.Empty;
        plan.Intent = AllowedIntents.Contains(requestedIntent)
            ? requestedIntent.Trim().ToLowerInvariant()
            : "lookup";

        var requestedMetric = (plan.Metric ?? string.Empty).Trim();
        var metric = Metrics.FirstOrDefault(m => m.Key.Equals(requestedMetric, StringComparison.OrdinalIgnoreCase));
        var questionMetric = ResolveMetric(Normalize(question));
        plan.Assumptions ??= [];
        if (metric is null ||
            (metric.Key == CustomMetric && questionMetric.Key != CustomMetric))
        {
            metric = questionMetric;
            plan.Assumptions.Add($"Måttet '{requestedMetric}' normaliserades till '{metric.Key}'.");
        }
        plan.Metric = metric.Key;

        var allowedDimensions = Dimensions.Select(d => d.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        plan.Dimensions = (plan.Dimensions ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d) && allowedDimensions.Contains(d.Trim()))
            .Select(d => d.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        plan.Filters = (plan.Filters ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f.Field) && !string.IsNullOrWhiteSpace(f.Value))
            .Take(10)
            .Select(f => new AiQueryPlanFilter
            {
                Field = Trim(f.Field, 80),
                Operator = NormalizeOperator(f.Operator),
                Value = Trim(f.Value, 160)
            })
            .ToList();

        plan.Period = TrimNullable(plan.Period, 80);
        plan.Comparison = NormalizeAllowed(plan.Comparison, AllowedComparisons);
        plan.TimeGrain = NormalizeAllowed(plan.TimeGrain, AllowedTimeGrains);
        plan.ResultContract ??= new AiQueryResultContract();
        plan.ResultContract.Shape = NormalizeShape(plan.ResultContract.Shape, plan.Intent);
        plan.ResultContract.RequiredRoles = (plan.ResultContract.RequiredRoles ?? [])
            .Where(role => !string.IsNullOrWhiteSpace(role) && AllowedResultRoles.Contains(role.Trim()))
            .Select(role => role.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
        plan.ResultContract.PreferredVisualization = NormalizeVisualization(
            plan.ResultContract.PreferredVisualization,
            plan.Intent);

        if (plan.Intent == "comparison")
        {
            plan.Comparison ??= "current_vs_previous_same_period";
            AddResultRole(plan.ResultContract, "current_period");
            AddResultRole(plan.ResultContract, "previous_period");
            AddResultRole(plan.ResultContract, "difference");
            plan.ResultContract.Shape = "single_row";
        }
        else if (plan.Intent == "trend")
        {
            AddResultRole(plan.ResultContract, "time");
            AddResultRole(plan.ResultContract, "metric");
        }
        else if (plan.Intent == "ranking")
        {
            AddResultRole(plan.ResultContract, "dimension");
            AddResultRole(plan.ResultContract, "metric");
        }
        else if (plan.Intent == "aggregate")
        {
            AddResultRole(plan.ResultContract, "metric");
            if (plan.Dimensions.Count > 0)
                AddResultRole(plan.ResultContract, "dimension");
        }
        plan.Sort = NormalizeSort(plan.Sort);
        plan.Limit = plan.Limit.HasValue ? Math.Clamp(plan.Limit.Value, 1, 200) : null;
        plan.Assumptions = (plan.Assumptions ?? [])
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => Trim(a, 200))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        return new AiQueryPlanValidation(true, plan);
    }

    public string? GetMetricLabel(string? metricKey) =>
        Metrics.FirstOrDefault(m => m.Key.Equals(metricKey, StringComparison.OrdinalIgnoreCase))?.Label;

    private static MetricDefinition ResolveMetric(string normalizedQuestion) =>
        Metrics
            .Where(m => !m.Key.Equals(CustomMetric, StringComparison.OrdinalIgnoreCase))
            .Select(m => new
            {
                Metric = m,
                Score = m.Synonyms.Count(s => normalizedQuestion.Contains(s, StringComparison.OrdinalIgnoreCase))
            })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault(x => x.Score > 0)?.Metric
        ?? Metrics.Single(m => m.Key == CustomMetric);

    private static string? ResolvePeriod(string question)
    {
        if (Regex.IsMatch(question, @"\b(i år|innevarande år|current year)\b"))
            return "current_year";
        if (Regex.IsMatch(question, @"\b(förra året|föregående år|last year)\b"))
            return "previous_year";
        if (Regex.IsMatch(question, @"\b(senaste 12 månader(?:na)?|last 12 months)\b"))
            return "last_12_months";
        if (Regex.IsMatch(question, @"\b(i månad|innevarande månad|current month)\b"))
            return "current_month";
        return null;
    }

    private static string? ResolveComparison(string question) =>
        Regex.IsMatch(
            question,
            @"\b(förra årets?|föregående års?|fjol|last year|previous year)\b") &&
        Regex.IsMatch(
            question,
            @"\b(i år|årets|innevarande år|hittills|ytd|samma period|samma månader|mot förra|mot föregående|vs\.?\s+(?:förra|föregående)|hur ligger vi till)\b")
            ? "current_vs_previous_same_period"
            : null;

    private static AiQueryResultContract BuildDefaultResultContract(
        string intent,
        string? comparison,
        IReadOnlyCollection<string> dimensions)
    {
        if (intent == "comparison" || comparison is not null)
        {
            return new AiQueryResultContract
            {
                Shape = "single_row",
                RequiredRoles = ["current_period", "previous_period", "difference"],
                PreferredVisualization = "comparison"
            };
        }

        if (intent == "trend")
        {
            return new AiQueryResultContract
            {
                Shape = "series",
                RequiredRoles = ["time", "metric"],
                PreferredVisualization = "line"
            };
        }

        if (intent == "ranking")
        {
            return new AiQueryResultContract
            {
                Shape = "table",
                RequiredRoles = ["dimension", "metric"],
                PreferredVisualization = "bar"
            };
        }

        return new AiQueryResultContract
        {
            Shape = dimensions.Count == 0 ? "single_row" : "table",
            RequiredRoles = dimensions.Count == 0 ? ["metric"] : ["dimension", "metric"],
            PreferredVisualization = dimensions.Count == 0 ? "kpi" : "table"
        };
    }

    private static string? NormalizeAllowed(string? value, HashSet<string> allowed)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return allowed.Contains(normalized) && normalized != "none" ? normalized : null;
    }

    private static string NormalizeShape(string? value, string intent)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is "single_row" or "table" or "series")
            return normalized;
        return intent is "aggregate" or "comparison" ? "single_row" : "table";
    }

    private static string NormalizeVisualization(string? value, string intent)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is "kpi" or "table" or "bar" or "line" or "comparison")
            return normalized;
        return intent switch
        {
            "comparison" => "comparison",
            "trend" => "line",
            "ranking" => "bar",
            "aggregate" => "kpi",
            _ => "table"
        };
    }

    private static void AddResultRole(AiQueryResultContract contract, string role)
    {
        if (!contract.RequiredRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            contract.RequiredRoles.Add(role);
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeOperator(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "equals" or "not_equals" or "contains" or "greater_than" or "less_than" or "between" or "in" =>
                value!.Trim().ToLowerInvariant(),
            _ => "equals"
        };

    private static string? NormalizeSort(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "ascending" => "ascending",
            "descending" => "descending",
            _ => null
        };

    private static string Trim(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? TrimNullable(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : Trim(value, maxLength);

    private sealed record MetricDefinition(
        string Key,
        string Label,
        string? Unit,
        string? Aggregation,
        IReadOnlyList<string> Synonyms,
        IReadOnlyList<string> ColumnHints);

    private sealed record DimensionDefinition(
        string Key,
        string Label,
        IReadOnlyList<string> Synonyms);
}

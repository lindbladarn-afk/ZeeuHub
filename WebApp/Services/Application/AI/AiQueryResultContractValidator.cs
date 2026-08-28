// Validates that a SQL result fulfills the semantic roles promised by its query plan.
using WebApp.Models.AI;

namespace WebApp.Services.Application.AI;

public static class AiQueryResultContractValidator
{
    private static readonly IReadOnlyDictionary<string, string[]> RoleAliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["metric"] = ["totalomsatt", "revenue", "amount", "value", "total", "antal", "count"],
            ["dimension"] = ["customer", "customername", "product", "productname", "month", "year", "date", "name"],
            ["time"] = ["month", "year", "date", "period"],
            ["current_period"] = ["currentyeartodate", "currentperiod", "currentvalue", "currentrevenue"],
            ["previous_period"] = ["previousyeartodate", "previousperiod", "previousvalue", "previousrevenue"],
            ["difference"] = ["difference", "diff", "change", "variance", "delta"]
        };

    public static AiQueryResultContractValidation Validate(SqlQueryResult query, AiQueryPlan? plan)
    {
        if (!query.Success)
            return AiQueryResultContractValidation.Failed("Databasfrågan misslyckades.");

        var requiredRoles = BuildRequiredRoles(plan);
        if (requiredRoles.Count == 0)
            return AiQueryResultContractValidation.Valid;

        if (query.RowCount == 0 || query.Rows.Count == 0)
            return AiQueryResultContractValidation.Failed("Resultatet saknar rader.");

        var normalizedColumns = query.Columns
            .Select(Normalize)
            .Where(column => column.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingRoles = requiredRoles
            .Where(role => !HasRole(normalizedColumns, role))
            .ToList();

        if (missingRoles.Count > 0)
        {
            return AiQueryResultContractValidation.Failed(
                $"Resultatet saknar obligatoriska roller: {string.Join(", ", missingRoles)}. " +
                $"Returnerade kolumner: {string.Join(", ", query.Columns)}.");
        }

        if (string.Equals(plan?.ResultContract?.Shape, "single_row", StringComparison.OrdinalIgnoreCase) &&
            query.RowCount != 1)
        {
            return AiQueryResultContractValidation.Failed(
                $"Resultatet skulle innehålla exakt en rad men innehöll {query.RowCount}.");
        }

        return AiQueryResultContractValidation.Valid;
    }

    private static List<string> BuildRequiredRoles(AiQueryPlan? plan)
    {
        if (plan is null)
            return [];

        var roles = (plan.ResultContract?.RequiredRoles ?? [])
            .Where(role => RoleAliases.ContainsKey(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.Equals(plan.Intent, "comparison", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(plan.Comparison, "current_vs_previous_same_period", StringComparison.OrdinalIgnoreCase))
        {
            AddRole(roles, "current_period");
            AddRole(roles, "previous_period");
            AddRole(roles, "difference");
        }
        else if (string.Equals(plan.Intent, "trend", StringComparison.OrdinalIgnoreCase))
        {
            AddRole(roles, "time");
            AddRole(roles, "metric");
        }
        else if (string.Equals(plan.Intent, "ranking", StringComparison.OrdinalIgnoreCase))
        {
            AddRole(roles, "dimension");
            AddRole(roles, "metric");
        }

        return roles;
    }

    private static bool HasRole(HashSet<string> columns, string role)
    {
        if (!RoleAliases.TryGetValue(role, out var aliases))
            return true;

        return aliases.Any(alias =>
            columns.Any(column =>
                column.Equals(alias, StringComparison.OrdinalIgnoreCase) ||
                column.Contains(alias, StringComparison.OrdinalIgnoreCase)));
    }

    private static void AddRole(List<string> roles, string role)
    {
        if (!roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            roles.Add(role);
    }

    private static string Normalize(string? value) =>
        new((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
}

public sealed record AiQueryResultContractValidation(bool Success, string? Error)
{
    public static AiQueryResultContractValidation Valid { get; } = new(true, null);

    public static AiQueryResultContractValidation Failed(string error) => new(false, error);
}

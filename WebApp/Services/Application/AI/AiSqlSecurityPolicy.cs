// Keeps AI reads inside the server-selected database without interpreting business-level SQL.
using System.Text.RegularExpressions;

namespace WebApp.Services.Application.AI;

public sealed class AiSqlSecurityPolicy : IAiSqlSecurityPolicy
{
    private static readonly Regex ExternalDataSource = new(
        @"(?is)\b(openrowset|opendatasource|openquery|bulk|waitfor|dbcc)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CrossDatabaseTableReference = new(
        @"(?is)\b(?:from|join)\s+(?:\[[^\]]+\]|[a-zA-Z0-9_]+)\s*\.\s*(?:\[[^\]]+\]|[a-zA-Z0-9_]+)\s*\.\s*(?:\[[^\]]+\]|[a-zA-Z0-9_]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public AiSqlPolicyResult Validate(string sql)
    {
        var normalizedSql = (sql ?? string.Empty).Trim();
        if (normalizedSql.Length == 0)
            return Fail(normalizedSql, "empty_sql", "SQL-planen saknar en fråga.");

        if (ExternalDataSource.IsMatch(normalizedSql))
        {
            return Fail(
                normalizedSql,
                "external_data_source",
                "SQL-planen försöker använda en extern datakälla eller serverfunktion.");
        }

        if (CrossDatabaseTableReference.IsMatch(normalizedSql))
        {
            return Fail(
                normalizedSql,
                "cross_database_access",
                "SQL-planen försöker läsa utanför den valda databasen.");
        }

        return new AiSqlPolicyResult(true, normalizedSql);
    }

    private static AiSqlPolicyResult Fail(string sql, string code, string message) =>
        new(false, sql, code, message);
}

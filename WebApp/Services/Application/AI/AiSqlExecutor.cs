using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WebApp.Models.AI;

namespace WebApp.Services.Application.AI
{
    public sealed class AiSqlExecutor : IAiSqlExecutor
    {
        // Strikt filter för AI/user-frågor: blockera farliga verb och kommentarer.
        // Semikolon hanteras separat (tillåt trailing ';' men blockera multi-statements).
        private static readonly Regex ForbiddenTokensStrict = new(
            pattern: @"(--|/\*|\*/|\b(drop|truncate|delete|update|insert|into|merge|alter|create|exec|execute|grant|revoke|set|declare|use|backup|restore|openrowset|opendatasource|openquery|bulk|waitfor|dbcc|go)\b)",
            options: RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Mildare filter för intern schemaläsning: tillåt ; och kommentarer, men blockera farliga verb
        private static readonly Regex ForbiddenTokensSchema = new(
            pattern: @"\b(drop|truncate|delete|update|insert|merge|alter|create|exec|execute|grant|revoke|set|declare|go)\b",
            options: RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Used to inject TOP safely for plain SELECT statements.
        private static readonly Regex SelectHead = new(
            pattern: @"^\s*select\s+(distinct\s+)?",
            options: RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public async Task<SqlQueryResult> ExecuteSelectAsync(
            string connectionString,
            string sql,
            int maxRows = 200,
            CancellationToken ct = default,
            bool allowSchemaIntrospection = false)
        {
            if (maxRows <= 0) maxRows = 200;

            if (string.IsNullOrWhiteSpace(connectionString))
                return Fail("Missing connection string.", sql ?? "");

            if (string.IsNullOrWhiteSpace(sql))
                return Fail("Empty SQL.", sql ?? "");

            var trimmed = NormalizeSql(sql);

            var isSelect = trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase);
            var isCte = trimmed.StartsWith("with", StringComparison.OrdinalIgnoreCase);

            // SELECT/CTE-only enforcement
            if (!isSelect && !isCte)
                return Fail("Only SELECT/CTE queries are allowed.", trimmed);

            // Allow one or more trailing ';' but reject any remaining ';' in the query body.
            // Remaining semicolon usually means multi-statement SQL, which we don't allow.
            if (trimmed.Contains(';'))
                return Fail("Multiple statements are not allowed.", trimmed);

            // Token filtering
            if (!allowSchemaIntrospection)
            {
                if (ForbiddenTokensStrict.IsMatch(trimmed))
                    return Fail("Query contains forbidden tokens.", trimmed);
            }
            else
            {
                if (ForbiddenTokensSchema.IsMatch(trimmed))
                    return Fail("Query contains forbidden tokens.", trimmed);
            }

            // Prefer server-side limiting:
            // - Plain SELECT => inject TOP(maxRows) if not already present.
            // - CTE => avoid rewriting and truncate after reading.
            var executedSql = isSelect ? EnsureTop(trimmed, maxRows) : trimmed;

            var result = new SqlQueryResult
            {
                ExecutedSql = executedSql
            };

            try
            {
                await using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync(ct);

                await using var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 60;
                cmd.CommandText = executedSql;

                await using var reader = await cmd.ExecuteReaderAsync(ct);

                for (int i = 0; i < reader.FieldCount; i++)
                    result.Columns.Add(reader.GetName(i));

                var rows = 0;
                while (await reader.ReadAsync(ct))
                {
                    var row = new System.Collections.Generic.List<object?>(reader.FieldCount);
                    for (int i = 0; i < reader.FieldCount; i++)
                        row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i));

                    result.Rows.Add(row);
                    rows++;

                    if (isCte && rows >= maxRows)
                        break;
                }

                result.Success = true;
                result.RowCount = rows;
                result.Truncated = rows >= maxRows;

                return result;
            }
            catch (Exception ex)
            {
                return Fail(ex.Message, executedSql);
            }
        }

        private static SqlQueryResult Fail(string msg, string executedSql) => new()
        {
            Success = false,
            Error = msg,
            ExecutedSql = executedSql
        };

        private static string NormalizeSql(string sql)
        {
            var normalized = (sql ?? string.Empty).Trim();

            if (normalized.Length == 0)
                return string.Empty;

            normalized = normalized.Replace("```sql", "", StringComparison.OrdinalIgnoreCase)
                                   .Replace("```", "", StringComparison.OrdinalIgnoreCase)
                                   .Trim();

            while (normalized.EndsWith(";", StringComparison.Ordinal))
                normalized = normalized[..^1].TrimEnd();

            return normalized;
        }

        private static string EnsureTop(string sql, int maxRows)
        {
            // Already has TOP(...)
            if (Regex.IsMatch(sql, @"^\s*select\s+(distinct\s+)?top\s*\(", RegexOptions.IgnoreCase))
                return sql;

            // If query uses OFFSET/FETCH, don't inject TOP (SQL Server forbids TOP + OFFSET).
            if (Regex.IsMatch(sql, @"(?is)\boffset\b"))
            {
                // Clamp FETCH NEXT if present.
                var fetchMatch = Regex.Match(sql, @"(?is)\bfetch\s+next\s+(?<n>\d+)\s+rows\s+only");
                if (fetchMatch.Success && int.TryParse(fetchMatch.Groups["n"].Value, out var n) && n > maxRows)
                {
                    return Regex.Replace(sql, @"(?is)\bfetch\s+next\s+\d+\s+rows\s+only",
                        $"FETCH NEXT {maxRows} ROWS ONLY");
                }

                // If OFFSET is the tail, append a safe FETCH NEXT limit.
                var offsetTail = Regex.Match(sql, @"(?is)\boffset\s+(?<n>\d+)\s+rows\s*;?\s*$");
                if (offsetTail.Success)
                {
                    var offset = offsetTail.Groups["n"].Value;
                    return Regex.Replace(sql, @"(?is)\boffset\s+\d+\s+rows\s*;?\s*$",
                        $"OFFSET {offset} ROWS FETCH NEXT {maxRows} ROWS ONLY");
                }

                return sql;
            }

            // Insert TOP(maxRows) right after SELECT or SELECT DISTINCT
            return SelectHead.Replace(sql, m =>
            {
                var distinctPart = m.Groups[1].Value;
                return $"SELECT {distinctPart}TOP ({maxRows}) ";
            }, count: 1);
        }
    }
}

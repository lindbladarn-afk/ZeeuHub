using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WebApp.Models.AI;

namespace WebApp.Services.Application.AI;

// This partial contains schema loading, schema focusing, and SQL draft generation.
// It keeps database introspection and LLM SQL prompting separate from the main request flow.
public sealed partial class AiDbChatOrchestrator
{
    private static readonly HashSet<string> EntityListSchemaColumns = new(
        [
            "ForetagKod", "CompanyCode", "Jeeves Company",
            "FtgNr", "FtgNamn", "FtgKundKod", "FtgLevKod", "FtgPostAdr3",
            "KundKredLim",
            "ArtNr", "ArtBeskr", "ArtBeskr2", "ArtKat",
            "Customer No", "Customer", "Customer Name", "CU_PK",
            "Item No", "Item", "Item Description", "AR_PK",
            "Supplier No", "Supplier", "Supplier Name", "SU_PK",
            // Keep the fields needed by deterministic rankings even when a wide BI view is trimmed.
            "Invoice Date", "Invoice Row SUM", "Row Amount Currency", "BestValue",
            "SalesAmount", "LineTotal", "Revenue", "Amount", "Belopp",
            "Order Date", "OrderQty", "Quantity", "Qty", "Units"
        ],
        StringComparer.OrdinalIgnoreCase);

    private async Task<(bool Success, string? Error, string? SchemaText)> LoadSchemaAsync(string conn, string cacheKey, CancellationToken ct)
    {
        if (_schemaCache.TryGetValue(cacheKey, out var cached) &&
            cached.CacheTime.AddMinutes(CacheDurationMinutes) > DateTime.UtcNow)
        {
            return (true, null, cached.SchemaText);
        }

        var result = await FetchSchemaFromDatabaseAsync(conn, ct);

        if (result.Success)
            _schemaCache[cacheKey] = (result.SchemaText!, DateTime.UtcNow);

        return result;
    }

    private async Task<(bool Success, string? Error, string? SchemaText)> FetchSchemaFromDatabaseAsync(string conn, CancellationToken ct)
    {
        const int maxTablesBySize = 25;
        const int maxPinnedTables = 64;
        const int maxColumnsPerTable = 50;

        var pinnedList = PinnedTables
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxPinnedTables)
            .ToArray();

        var pinnedValuesSql = pinnedList.Length == 0
            ? "SELECT CAST(NULL AS sysname) AS SchemaName, CAST(NULL AS sysname) AS TableName WHERE 1=0"
            : "SELECT v.SchemaName, v.TableName FROM (VALUES " +
              string.Join(", ", pinnedList.Select(t =>
              {
                  var parts = t.Split('.', 2);
                  var s = parts.Length == 2 ? parts[0] : "dbo";
                  var n = parts.Length == 2 ? parts[1] : parts[0];
                  s = s.Replace("'", "''");
                  n = n.Replace("'", "''");
                  return $"(N'{s}', N'{n}')";
              })) +
              ") AS v(SchemaName, TableName)";

        var schemaSql = $@"
WITH pinned AS (
    {pinnedValuesSql}
),
big AS (
    SELECT TOP ({maxTablesBySize})
        o.object_id AS ObjectId,
        s.name      AS SchemaName,
        o.name      AS TableName,
        SUM(p.rows) AS [TotalRows]
    FROM sys.objects o
    JOIN sys.schemas s ON s.schema_id = o.schema_id
    JOIN sys.partitions p ON p.object_id = o.object_id AND p.index_id IN (0,1)
    WHERE o.type = 'U'
    GROUP BY o.object_id, s.name, o.name
    ORDER BY SUM(p.rows) DESC
),
picked AS (
    SELECT b.ObjectId, b.SchemaName, b.TableName, b.TotalRows
    FROM big b
    UNION
    SELECT o.object_id AS ObjectId, s.name AS SchemaName, o.name AS TableName, ISNULL(SUM(p.rows), 0) AS TotalRows
    FROM sys.objects o
    JOIN sys.schemas s ON s.schema_id = o.schema_id
    LEFT JOIN sys.partitions p ON p.object_id = o.object_id AND p.index_id IN (0,1)
    JOIN pinned pt ON pt.SchemaName = s.name AND pt.TableName = o.name
    WHERE o.type IN ('U', 'V')
    GROUP BY o.object_id, s.name, o.name
),
c_base AS (
    SELECT
        c.object_id,
        c.name AS ColumnName,
        ty.name AS DataType,
        c.is_nullable AS IsNullable,
        c.column_id,
        CASE WHEN ty.name IN ('varbinary','image','xml','geometry','geography','hierarchyid') THEN 1 ELSE 0 END AS IsBinary
    FROM sys.columns c
    JOIN sys.types ty ON ty.user_type_id = c.user_type_id
),
pks AS (
    SELECT c.object_id, c.name AS ColumnName
    FROM sys.index_columns ic
    JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE i.is_primary_key = 1
)
SELECT
    p.SchemaName,
    p.TableName,
    cb.ColumnName,
    cb.DataType,
    cb.IsNullable,
    p.[TotalRows],
    CASE WHEN pks.ColumnName IS NOT NULL THEN 'PK' ELSE NULL END AS KeyType
FROM picked p
JOIN c_base cb ON cb.object_id = p.ObjectId
LEFT JOIN pks ON pks.object_id = p.ObjectId AND pks.ColumnName = cb.ColumnName
WHERE cb.IsBinary = 0
ORDER BY p.[TotalRows] DESC, p.SchemaName, p.TableName, cb.column_id
";

        // ✅ Viktigt: schema-introspection
        var res = await _sql.ExecuteSelectAsync(conn, schemaSql, maxRows: 20000, ct: ct, allowSchemaIntrospection: true);
        if (!res.Success)
            return (false, res.Error, null);

        var byTable = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in res.Rows)
        {
            var schemaName = row.Count > 0 ? row[0]?.ToString() ?? "dbo" : "dbo";
            var tableName  = row.Count > 1 ? row[1]?.ToString() ?? "" : "";
            var colName    = row.Count > 2 ? row[2]?.ToString() ?? "" : "";
            var dataType   = row.Count > 3 ? row[3]?.ToString() ?? "" : "";
            var isNullable = row.Count > 4 ? row[4]?.ToString() ?? "" : "";
            var keyType    = row.Count > 6 ? row[6]?.ToString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(colName))
                continue;

            var key = $"{schemaName}.{tableName}";
            if (!byTable.TryGetValue(key, out var cols))
            {
                cols = new List<string>();
                byTable[key] = cols;
            }

            if (cols.Count >= maxColumnsPerTable && !EntityListSchemaColumns.Contains(colName))
                continue;

            var nullMark = (isNullable == "True" || isNullable == "1") ? "NULL" : "NOT NULL";
            var keyMark = string.IsNullOrWhiteSpace(keyType) ? string.Empty : $" [{keyType}]";

            cols.Add($"{colName} {dataType} {nullMark}{keyMark}".Trim());
        }

        if (byTable.Count == 0)
            return (false, "No tables found.", null);

        var fkSql = @"
SELECT
    sch_from.name AS FromSchema,
    t_from.name   AS FromTable,
    c_from.name   AS FromColumn,
    sch_to.name   AS ToSchema,
    t_to.name     AS ToTable,
    c_to.name     AS ToColumn
FROM sys.foreign_key_columns fkc
JOIN sys.tables t_from ON t_from.object_id = fkc.parent_object_id
JOIN sys.schemas sch_from ON sch_from.schema_id = t_from.schema_id
JOIN sys.columns c_from ON c_from.object_id = fkc.parent_object_id AND c_from.column_id = fkc.parent_column_id
JOIN sys.tables t_to ON t_to.object_id = fkc.referenced_object_id
JOIN sys.schemas sch_to ON sch_to.schema_id = t_to.schema_id
JOIN sys.columns c_to ON c_to.object_id = fkc.referenced_object_id AND c_to.column_id = fkc.referenced_column_id
";

        // ✅ Viktigt: schema-introspection
        var fkRes = await _sql.ExecuteSelectAsync(conn, fkSql, maxRows: 5000, ct: ct, allowSchemaIntrospection: true);

        var sb = new StringBuilder();
        sb.AppendLine("AVAILABLE TABLES & COLUMNS (SQL Server) [PK=Primary Key]:");

        var pinnedSet = new HashSet<string>(PinnedTables, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in byTable
                     .OrderByDescending(kv => pinnedSet.Contains(kv.Key))
                     .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"- {kv.Key} ({string.Join(", ", kv.Value)})");
        }

        if (fkRes.Success && fkRes.Rows.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("FOREIGN KEY RELATIONSHIPS (join hints):");
            foreach (var row in fkRes.Rows)
            {
                var fromSchema = row.Count > 0 ? row[0]?.ToString() ?? "" : "";
                var fromTable  = row.Count > 1 ? row[1]?.ToString() ?? "" : "";
                var fromCol    = row.Count > 2 ? row[2]?.ToString() ?? "" : "";
                var toSchema   = row.Count > 3 ? row[3]?.ToString() ?? "" : "";
                var toTable    = row.Count > 4 ? row[4]?.ToString() ?? "" : "";
                var toCol      = row.Count > 5 ? row[5]?.ToString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(fromTable) || string.IsNullOrWhiteSpace(toTable)) continue;
                sb.AppendLine($"- {fromSchema}.{fromTable}.{fromCol} -> {toSchema}.{toTable}.{toCol}");
            }
        }

        return (true, null, sb.ToString());
    }
    private async Task<SqlDraftResult> GenerateSqlAsync(string question, string schemaText, int? companyCode, string dataSourceKey, string dataProfile, string dbMemoryKey, TokenUsageTotals tokenUsage, CancellationToken ct)
    {
        var systemBase = @"
You are a senior SQL Server analyst.
Your job: produce a structured query plan and ONE safe, optimized, read-only SQL Server query based on the user's request and the provided schema.

RULES (must follow):
- Output ONLY valid JSON (no markdown, no code fences, no commentary).
- JSON schema:
  {
    ""plan"": {
      ""intent"": ""lookup|aggregate|ranking|trend|comparison|detail"",
      ""metric"": ""<semantic metric key>"",
      ""dimensions"": [""<semantic dimension key>""],
      ""filters"": [{ ""field"": ""<schema field>"", ""operator"": ""equals|not_equals|contains|greater_than|less_than|between|in"", ""value"": ""<value>"" }],
      ""period"": ""<normalized period or null>"",
      ""comparison"": ""none|current_vs_previous_same_period|period_over_period|actual_vs_budget"",
      ""time_grain"": ""day|week|month|quarter|year|null"",
      ""result_contract"": {
        ""shape"": ""single_row|table|series"",
        ""required_roles"": [""metric|dimension|time|current_period|previous_period|difference""],
        ""preferred_visualization"": ""kpi|table|bar|line|comparison""
      },
      ""sort"": ""ascending|descending|null"",
      ""limit"": <1..200 or null>,
      ""assumptions"": [""<short explicit assumption>""]
    },
    ""sql"": ""<SQL or empty string>"",
    ""requires_clarification"": <true|false>,
    ""reason"": ""<short reason>""
  }
- If you can generate SQL, set requires_clarification=false and include SQL in sql.
- If the question is ambiguous or impossible with given schema, set requires_clarification=true and explain why in reason.
- ONLY SELECT queries or CTE (WITH ... SELECT). No multi-statements.
- Do not use INSERT/UPDATE/DELETE/MERGE/ALTER/DROP/CREATE/EXEC or any modifying statement.
- Use only tables/columns that exist in the provided schema.
- Always wrap schema, table and column identifiers in SQL Server brackets (e.g. [dbo].[q_zu_bi_fsg], [Invoice Row SUM], [Company]).
- You MAY use TOP (N) when the user explicitly asks for a limited number of rows (e.g., 5, 10). N must be <= 200.
- Ensure all output columns are explicitly aliased (e.g., COUNT(t1.Id) AS TotalCount).
- The SQL result MUST satisfy plan.result_contract.
- Use canonical aliases for result roles: [MetricValue], [Dimension], [Period], [CurrentPeriod], [PreviousPeriod], [Difference].
- For current-year actuals, ""i år"" and ""årets"" mean January 1 through today. Do not include future-dated rows unless the user explicitly asks for forecast, planned or full-calendar-year data.
- For current_vs_previous_same_period, use the same inclusive end date in both years and return exactly one row with [CurrentPeriod], [PreviousPeriod] and [Difference].
- CRITICAL: NEVER use 'RowCount' as an alias name; use 'TotalCount', 'Antal' or 'RowsFound' instead.
- When using date/time literals, always use ISO 8601 'YYYY-MM-DD' or 'YYYY-MM-DD HH:MM:SS'.
- Prefer correct JOIN logic using PK/FK info and column names listed in the schema.
- If the question contains ""omsättning"", ""omsatta"", ""intäkt"" or ""revenue"", prefer monetary columns (e.g., LineTotal, SalesAmount).
- If the question contains ""antal"", ""sålda"", ""enheter"" or ""quantity"", prefer quantity columns (e.g., OrderQty, Quantity).
- If `dbo.q_zu_bi_fsg` exists in the provided schema and the question is about invoices/revenue/sales, prefer `dbo.q_zu_bi_fsg` over legacy tables like `dbo.ft`.
- If `dbo.q_zu_bi_item` exists and product/item attributes are needed, prefer joining to `dbo.q_zu_bi_item`.
- If `dbo.q_zu_bi_customer` exists and customer attributes are needed, prefer joining to `dbo.q_zu_bi_customer`.
- For credit-limit questions, use `[dbo].[kus].[kundkredlim]` when that table and column are present. Never use `[dbo].[fr].[aktiekap]` as a credit limit; it is not a customer credit-limit field.
- If both `dbo.q_zu_bi_fsg` and `dbo.q_zu_bi_item` contain `[AR_PK]`, join them on `[AR_PK]` instead of composite joins on company/item columns.
- When joining fact and dimension tables, always prefer shared PK fields (e.g. `[AR_PK]`, `[CU_PK]`) over composite business-key joins.
- If the question asks for top/best/most and no time period is specified, do NOT add date filters.
- CompanyCode in CONTEXT is a helpful default for questions about the active company, not a mandatory filter.
- If the user explicitly asks across companies in the selected database, respect that request.
- Conversation history may include LATEST DATABASE RESULT CONTEXT. For a follow-up that refers to a prior row, use its exact identifier as a filter instead of running a broad new query. Ask a clarification only when more than one prior row could be meant.
";

        var profileRule = string.Equals(dataProfile, AiDataProfile.DataWarehouse, StringComparison.OrdinalIgnoreCase)
            ? "DATA PROFILE: DataWarehouse. Prefer q_zu_bi_* fact and dimension views. Do not use raw Jeeves tables (ft, fh, oh, orp, fr, kus) when an equivalent warehouse view is available."
            : "DATA PROFILE: JeevesDirect. Prefer raw Jeeves tables and their documented joins. Do not use q_zu_bi_* views unless the user explicitly asks for warehouse data.";

        var ordersHints = await TryLoadKnowledgeAsync("AI/Knowledge/db/jeeves/jeeves-orders.md", ct);
        var invoicesHints = await TryLoadKnowledgeAsync("AI/Knowledge/db/jeeves/jeeves-invoices.md", ct);
        var customersHints = await TryLoadKnowledgeAsync("AI/Knowledge/db/jeeves/jeeves-customers.md", ct);
        var metricHints = await TryLoadKnowledgeAsync("AI/Knowledge/db/jeeves/metrics.md", ct);
        var joinHints = await TryLoadKnowledgeAsync("AI/Knowledge/db/jeeves/joins.md", ct);
        var synonymHints = await TryLoadKnowledgeAsync("AI/Knowledge/db/jeeves/synonyms.yml", ct);

        var combinedHints = string.Join(
            Environment.NewLine + Environment.NewLine,
            new[] { ordersHints, invoicesHints, customersHints, metricHints, joinHints, synonymHints }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        var system = string.IsNullOrWhiteSpace(combinedHints)
            ? systemBase + Environment.NewLine + profileRule
            : systemBase + Environment.NewLine + profileRule + Environment.NewLine + Environment.NewLine +
              "DOMAIN HINTS (from AI knowledge base):" + Environment.NewLine +
              combinedHints;
        system += Environment.NewLine + Environment.NewLine + _semanticCatalog.BuildPromptContext();

        var focusedSchemaText = BuildFocusedSchemaText(question, schemaText);
        var focusedResult = await AskSqlDraftAsync(
            question,
            focusedSchemaText,
            companyCode,
            system,
            dbMemoryKey,
            tokenUsage,
            ct);

        if (!string.IsNullOrWhiteSpace(focusedResult.Sql))
            return focusedResult;

        // A focused schema may hide a valid path. Confirm an empty or clarification result
        // against the complete selected-database schema before asking the user to rephrase.
        var fullSchemaResult = await AskSqlDraftAsync(
            question,
            schemaText,
            companyCode,
            system,
            dbMemoryKey,
            tokenUsage,
            ct);
        if (!string.IsNullOrWhiteSpace(fullSchemaResult.Sql) || fullSchemaResult.RequiresClarification)
            return fullSchemaResult;

        return focusedResult.RequiresClarification ? focusedResult : fullSchemaResult;
    }

    private async Task<SqlDraftResult> AskSqlDraftAsync(
        string question,
        string schemaText,
        int? companyCode,
        string systemPrompt,
        string dbMemoryKey,
        TokenUsageTotals tokenUsage,
        CancellationToken ct,
        string? planQuestion = null)
    {
        var user = new StringBuilder();
        user.AppendLine(schemaText);
        user.AppendLine();
        user.AppendLine("USER QUESTION:");
        user.AppendLine(question);

        var followUpReference = BuildFollowUpReferenceHint(question, dbMemoryKey);
        if (!string.IsNullOrWhiteSpace(followUpReference))
        {
            user.AppendLine();
            user.AppendLine(followUpReference);
        }

        if (companyCode.HasValue)
        {
            user.AppendLine();
            user.AppendLine(
                $"CONTEXT: CompanyCode = {companyCode.Value}. " +
                "Use it as a default only when the question is clearly about the active company and the schema has a matching company column. " +
                "Do not force a company filter when the user asks across companies. " +
                "Never invent a ForetagKod column if it is not present in the schema.");
        }

        var result = await _chat.AskAsync(
            userMessage: user.ToString(),
            history: BuildHistory(systemPrompt, dbMemoryKey),
            ct: ct);
        tokenUsage.Add(result);

        var raw = (result.Answer ?? string.Empty).Trim();
        if (AiSqlResponseParser.TryParseStructuredQueryResponse(raw, out var parsed))
        {
            var planValidation = _semanticCatalog.ValidateAndNormalize(parsed.Plan, planQuestion ?? question);
            var normalizedSql = (parsed.Sql ?? string.Empty).Trim();
            if (parsed.RequiresClarification)
            {
                return new SqlDraftResult
                {
                    RequiresClarification = true,
                    Reason = parsed.Reason,
                    Plan = planValidation.Plan
                };
            }

            if (normalizedSql.StartsWith("select", StringComparison.OrdinalIgnoreCase) ||
                normalizedSql.StartsWith("with", StringComparison.OrdinalIgnoreCase))
            {
                return new SqlDraftResult
                {
                    Sql = normalizedSql,
                    Reason = parsed.Reason,
                    Plan = planValidation.Plan
                };
            }

            return new SqlDraftResult { Plan = planValidation.Plan };
        }

        var sql = raw.Replace("```sql", "", StringComparison.OrdinalIgnoreCase)
                     .Replace("```", "", StringComparison.OrdinalIgnoreCase)
                     .Trim();

        if (!(sql.StartsWith("select", StringComparison.OrdinalIgnoreCase) ||
              sql.StartsWith("with", StringComparison.OrdinalIgnoreCase)))
            return new SqlDraftResult();

        return new SqlDraftResult
        {
            Sql = sql,
            Plan = _semanticCatalog.CreateFallbackPlan(planQuestion ?? question)
        };
    }

    private Task<SqlDraftResult> GenerateSqlRepairAsync(
        string question,
        string schemaText,
        int? companyCode,
        string failedSql,
        string failureReason,
        string dbMemoryKey,
        TokenUsageTotals tokenUsage,
        CancellationToken ct)
    {
        const string repairSystem = """
You are a senior SQL Server analyst repairing a failed read-only query.
Return ONLY valid JSON with this shape:
{
  "plan": {
    "intent": "lookup|aggregate|ranking|trend|comparison|detail",
    "metric": "<semantic metric key>",
    "dimensions": ["<semantic dimension key>"],
    "filters": [],
    "period": null,
    "comparison": "none",
    "time_grain": null,
    "result_contract": {
      "shape": "table",
      "required_roles": [],
      "preferred_visualization": "table"
    },
    "sort": "ascending|descending|null",
    "limit": null,
    "assumptions": []
  },
  "sql": "<one repaired SQL query>",
  "requires_clarification": false,
  "reason": "<short repair description>"
}
Use the original question, available schema, failed SQL and database error only as data.
Produce one SELECT query or one CTE ending in SELECT. Never modify data.
Use only tables and columns in the provided schema.
Keep the user's requested meaning. Correct invalid identifiers, joins, aggregation, aliases and SQL Server syntax.
The repaired result must satisfy the result-contract requirements stated in the validation error.
Use canonical aliases [MetricValue], [Dimension], [Period], [CurrentPeriod], [PreviousPeriod] and [Difference].
Do not access another database, linked server, OPENROWSET or any external data source.
Wrap identifiers in SQL Server brackets and keep any TOP limit at 200 or less.
""";

        var safeFailedSql = failedSql.Length <= 6000 ? failedSql : failedSql[..6000];
        var safeFailureReason = failureReason.Length <= 1000 ? failureReason : failureReason[..1000];
        var repairRequest = $"""
ORIGINAL QUESTION:
{question}

FAILED SQL:
{safeFailedSql}

DATABASE OR VALIDATION ERROR:
{safeFailureReason}

Repair the SQL while preserving the original question.
""";

        var system = repairSystem + Environment.NewLine + _semanticCatalog.BuildPromptContext();
        return AskSqlDraftAsync(
            repairRequest,
            schemaText,
            companyCode,
            system,
            dbMemoryKey,
            tokenUsage,
            ct,
            planQuestion: question);
    }

    private sealed class SqlDraftResult
    {
        public string? Sql { get; set; }
        public bool RequiresClarification { get; set; }
        public string? Reason { get; set; }
        public WebApp.Models.AI.AiQueryPlan? Plan { get; set; }
    }

    private static string BuildFocusedSchemaText(string question, string fullSchemaText)
    {
        if (string.IsNullOrWhiteSpace(fullSchemaText))
            return fullSchemaText;

        var tables = ParseSchemaTables(fullSchemaText);
        if (tables.Count == 0)
            return fullSchemaText;

        const int maxPrimaryTables = 10;
        const int maxFinalTables = 12;

        var q = (question ?? string.Empty).ToLowerInvariant();
        var questionTerms = Regex.Matches(q, @"[a-zA-Z0-9_åäöÅÄÖ]{3,}")
            .Select(m => m.Value.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selected = tables.Keys
            .Select(t => new { Table = t, Score = ScoreTableForQuestion(t, tables[t], questionTerms, q) })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Table, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Score > 0)
            .Take(maxPrimaryTables)
            .Select(x => x.Table)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Domain-specific anchors that improve precision for common ZeeU BI questions.
        var normalizedQuestion = question ?? string.Empty;
        EnsureIfPresent(selected, tables, LooksLikeTopProductsQuestion(normalizedQuestion) || LooksLikeFactQuestion(normalizedQuestion), "dbo.q_zu_bi_fsg");
        EnsureIfPresent(selected, tables, LooksLikeTopProductsQuestion(normalizedQuestion), "dbo.q_zu_bi_item");
        EnsureIfPresent(selected, tables, LooksLikeTopCustomersQuestion(normalizedQuestion), "dbo.q_zu_bi_customer");
        EnsureIfPresent(selected, tables, LooksLikeTopCustomersQuestion(normalizedQuestion) || LooksLikeFactQuestion(normalizedQuestion), "dbo.q_zu_bi_fsg");

        if (selected.Count == 0)
        {
            foreach (var table in tables.Keys.Take(maxPrimaryTables))
                selected.Add(table);
        }

        var fkEdges = ParseSchemaForeignKeys(fullSchemaText);
        ExpandWithForeignKeyNeighbors(selected, fkEdges, maxFinalTables);

        var focusedTableLines = fullSchemaText
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(line =>
            {
                if (!line.StartsWith("- ", StringComparison.Ordinal) || line.Contains("->", StringComparison.Ordinal))
                    return false;

                var afterDash = line.Substring(2).Trim();
                var parenIdx = afterDash.IndexOf(" (", StringComparison.Ordinal);
                if (parenIdx < 0)
                    return false;

                var table = afterDash.Substring(0, parenIdx).Trim();
                return selected.Contains(table);
            })
            .ToList();

        if (focusedTableLines.Count == 0)
            return fullSchemaText;

        var focusedFkLines = fkEdges
            .Where(e => selected.Contains(e.FromTable) && selected.Contains(e.ToTable))
            .Select(e => $"- {e.FromTable}.{e.FromColumn} -> {e.ToTable}.{e.ToColumn}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("AVAILABLE TABLES & COLUMNS (SQL Server) [PK=Primary Key] - FOCUSED SUBSET:");
        foreach (var line in focusedTableLines.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            sb.AppendLine(line);

        if (focusedFkLines.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("FOREIGN KEY RELATIONSHIPS (join hints):");
            foreach (var line in focusedFkLines)
                sb.AppendLine(line);
        }

        return sb.ToString();
    }

    private static int ScoreTableForQuestion(string table, List<string> columns, List<string> questionTerms, string fullQuestion)
    {
        var score = 0;
        var tableLower = table.ToLowerInvariant();

        foreach (var term in questionTerms)
        {
            if (tableLower.Contains(term, StringComparison.OrdinalIgnoreCase))
                score += 5;
        }

        foreach (var col in columns)
        {
            var colLower = col.ToLowerInvariant();
            foreach (var term in questionTerms)
            {
                if (colLower.Contains(term, StringComparison.OrdinalIgnoreCase))
                    score += 2;
            }
        }

        if (LooksLikeFactQuestion(fullQuestion))
        {
            if (tableLower.Contains("fsg", StringComparison.OrdinalIgnoreCase) ||
                tableLower.Contains("sales", StringComparison.OrdinalIgnoreCase) ||
                tableLower.Contains("fact", StringComparison.OrdinalIgnoreCase) ||
                tableLower.Contains("invoice", StringComparison.OrdinalIgnoreCase) ||
                tableLower.Contains("order", StringComparison.OrdinalIgnoreCase))
            {
                score += 8;
            }
        }

        if (LooksLikeTopProductsQuestion(fullQuestion) &&
            (tableLower.Contains("item", StringComparison.OrdinalIgnoreCase) ||
             tableLower.Contains("product", StringComparison.OrdinalIgnoreCase)))
        {
            score += 8;
        }

        if (LooksLikeTopCustomersQuestion(fullQuestion) &&
            (tableLower.Contains("customer", StringComparison.OrdinalIgnoreCase) ||
             tableLower.Contains("kund", StringComparison.OrdinalIgnoreCase)))
        {
            score += 8;
        }

        return score;
    }

    private static void EnsureIfPresent(
        HashSet<string> selected,
        Dictionary<string, List<string>> tables,
        bool condition,
        string table)
    {
        if (!condition)
            return;
        if (tables.ContainsKey(table))
            selected.Add(table);
    }

    private static List<(string FromTable, string FromColumn, string ToTable, string ToColumn)> ParseSchemaForeignKeys(string schemaText)
    {
        var result = new List<(string FromTable, string FromColumn, string ToTable, string ToColumn)>();
        if (string.IsNullOrWhiteSpace(schemaText))
            return result;

        foreach (var raw in schemaText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (!line.StartsWith("- ", StringComparison.Ordinal) || !line.Contains("->", StringComparison.Ordinal))
                continue;

            var body = line.Substring(2).Trim();
            var parts = body.Split("->", 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                continue;

            var fromParts = parts[0].Split('.', StringSplitOptions.RemoveEmptyEntries);
            var toParts = parts[1].Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (fromParts.Length < 3 || toParts.Length < 3)
                continue;

            var fromTable = $"{fromParts[0]}.{fromParts[1]}";
            var toTable = $"{toParts[0]}.{toParts[1]}";
            var fromColumn = fromParts[2];
            var toColumn = toParts[2];
            result.Add((fromTable, fromColumn, toTable, toColumn));
        }

        return result;
    }

    private static void ExpandWithForeignKeyNeighbors(
        HashSet<string> selected,
        List<(string FromTable, string FromColumn, string ToTable, string ToColumn)> fkEdges,
        int maxTables)
    {
        if (selected.Count >= maxTables || fkEdges.Count == 0)
            return;

        var added = true;
        while (added && selected.Count < maxTables)
        {
            added = false;

            foreach (var edge in fkEdges)
            {
                if (selected.Count >= maxTables)
                    break;

                if (selected.Contains(edge.FromTable) && !selected.Contains(edge.ToTable))
                {
                    selected.Add(edge.ToTable);
                    added = true;
                    continue;
                }

                if (selected.Contains(edge.ToTable) && !selected.Contains(edge.FromTable))
                {
                    selected.Add(edge.FromTable);
                    added = true;
                }
            }
        }
    }
}

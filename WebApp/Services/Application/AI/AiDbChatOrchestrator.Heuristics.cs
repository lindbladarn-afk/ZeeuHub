using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WebApp.Services.Application.AI;

// This partial contains question heuristics and SQL fallback builders.
// It is pure orchestration support logic and does not change the external AI contract.
public sealed partial class AiDbChatOrchestrator
{
    private static readonly Regex SchemaColumnType = new(
        @"\s+(?:bigint|int|smallint|tinyint|bit|decimal|numeric|money|smallmoney|float|real|date|datetime2?|smalldatetime|datetimeoffset|time|uniqueidentifier|nvarchar|varchar|nchar|char|text|ntext|binary|varbinary|image|xml|timestamp|rowversion|sql_variant|geography|geometry|hierarchyid)\b(?:\s*\([^)]*\))?(?=\s+(?:NOT\s+NULL|NULL|\[[^\]]+\])|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> SimpleEntityListWords = new(
        [
            "visa", "lista", "hämta", "hamta", "ge", "mig", "ta", "fram",
            "show", "list", "get", "display", "my", "me",
            "mina", "alla", "samtliga", "all", "och", "and",
            "kund", "kunder", "kundnummer", "kundnr", "kundnamn",
            "customer", "customers", "customernumber", "customername",
            "artikel", "artiklar", "artikelnummer", "artikelnr", "artikelbeskrivning",
            "produkt", "produkter", "produktnummer", "produktnamn",
            "item", "items", "itemnumber", "itemdescription",
            "leverantör", "leverantörer", "leverantörsnummer", "leverantörsnr", "leverantörsnamn",
            "leverantor", "leverantorer", "leverantorsnummer", "leverantorsnr", "leverantorsnamn",
            "supplier", "suppliers", "suppliernumber", "suppliername"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static SimpleEntityListFallback? BuildSimpleEntityListFallback(
        string question,
        string? schemaText)
    {
        var entity = ClassifySimpleEntityList(question);
        if (entity is null || string.IsNullOrWhiteSpace(schemaText))
            return null;

        var tables = ParseDetailedSchemaTables(schemaText);
        if (tables.Count == 0)
            return null;

        return entity.Value switch
        {
            SimpleEntity.Customer => BuildCustomerListFallback(tables),
            SimpleEntity.Product => BuildProductListFallback(tables),
            SimpleEntity.Supplier => BuildSupplierListFallback(tables),
            _ => null
        };
    }

    private static SimpleEntity? ClassifySimpleEntityList(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return null;

        var normalized = (question ?? string.Empty).ToLowerInvariant();
        var words = Regex.Matches(normalized, @"[\p{L}\p{N}]+")
            .Select(match => match.Value)
            .ToList();
        if (words.Count == 0 || words.Any(word => !SimpleEntityListWords.Contains(word)))
            return null;

        if (Regex.IsMatch(
                normalized,
                @"(?is)\b(top(p)?|topp|störst(a|e)?|bäst(a|e)?|mest|minst|omsätt\w*|intäkt|försälj\w*|antal|summa|belopp|trend|utveckling|diagram|graf|jämför|per\s+\w+|månad|år|vecka|period)\b"))
        {
            return null;
        }

        if (words.Any(word =>
                word.StartsWith("kund", StringComparison.Ordinal) ||
                word.StartsWith("customer", StringComparison.Ordinal)))
            return SimpleEntity.Customer;

        if (words.Any(word =>
                word.StartsWith("artik", StringComparison.Ordinal) ||
                word.StartsWith("produkt", StringComparison.Ordinal) ||
                word.StartsWith("item", StringComparison.Ordinal)))
            return SimpleEntity.Product;

        if (words.Any(word =>
                word.StartsWith("leverantör", StringComparison.Ordinal) ||
                word.StartsWith("leverantor", StringComparison.Ordinal) ||
                word.StartsWith("supplier", StringComparison.Ordinal)))
            return SimpleEntity.Supplier;

        return null;
    }

    private static SimpleEntityListFallback? BuildCustomerListFallback(
        Dictionary<string, List<string>> tables)
    {
        var table = PickEntityTable(tables, "dbo.q_zu_bi_customer", "dbo.fr", "dim_customer", "customer");
        if (table is null)
            return null;

        var columns = tables[table];
        var id = FindEntityColumn(columns, "customer no", "customerno", "kundnr", "kundnummer", "ftgnr", "customerid", "cu_pk");
        var name = FindEntityColumn(columns, "customer name", "customername", "kundnamn", "ftgnamn", "customer", "name");
        var company = FindEntityColumn(columns, "jeeves company", "foretagkod", "companycode");
        var city = FindEntityColumn(columns, "city", "ort", "ftgpostadr3");
        var typeColumn = table.EndsWith(".fr", StringComparison.OrdinalIgnoreCase)
            ? FindEntityColumn(columns, "ftgkundkod")
            : null;

        return BuildEntityListSql(
            table,
            [
                (company, "CompanyCode"),
                (id, "CustomerNumber"),
                (name, "CustomerName"),
                (city, "City")
            ],
            id ?? name,
            name ?? id,
            typeColumn is null ? null : $"{QuoteSqlIdentifier(typeColumn)} = '1'",
            "kunder",
            "Kundregister");
    }

    private static SimpleEntityListFallback? BuildProductListFallback(
        Dictionary<string, List<string>> tables)
    {
        var table = PickEntityTable(tables, "dbo.q_zu_bi_item", "dbo.ar", "dim_product", "product");
        if (table is null)
            return null;

        var columns = tables[table];
        var id = FindEntityColumn(columns, "item no", "itemno", "artnr", "product no", "productno", "productid", "ar_pk");
        var name = FindEntityColumn(columns, "item description", "itemdescription", "artikelbeskrivning", "artbeskr", "product name", "productname", "item", "name");
        var company = FindEntityColumn(columns, "jeeves company", "foretagkod", "companycode");
        var category = FindEntityColumn(columns, "category", "kategori", "artkat");

        return BuildEntityListSql(
            table,
            [
                (company, "CompanyCode"),
                (id, "ItemNumber"),
                (name, "ItemDescription"),
                (category, "Category")
            ],
            id ?? name,
            name ?? id,
            additionalFilter: null,
            "artiklar",
            "Artikelregister");
    }

    private static SimpleEntityListFallback? BuildSupplierListFallback(
        Dictionary<string, List<string>> tables)
    {
        var table = PickEntityTable(tables, "dbo.q_zu_bi_supplier", "dbo.fr", "dim_supplier", "supplier");
        if (table is null)
            return null;

        var columns = tables[table];
        var id = FindEntityColumn(columns, "supplier no", "supplierno", "leverantörsnr", "leverantorsnr", "ftgnr", "supplierid", "su_pk");
        var name = FindEntityColumn(columns, "supplier name", "suppliername", "leverantörsnamn", "leverantorsnamn", "ftgnamn", "supplier", "name");
        var company = FindEntityColumn(columns, "jeeves company", "foretagkod", "companycode");
        var city = FindEntityColumn(columns, "city", "ort", "ftgpostadr3");
        var typeColumn = table.EndsWith(".fr", StringComparison.OrdinalIgnoreCase)
            ? FindEntityColumn(columns, "ftglevkod")
            : null;

        return BuildEntityListSql(
            table,
            [
                (company, "CompanyCode"),
                (id, "SupplierNumber"),
                (name, "SupplierName"),
                (city, "City")
            ],
            id ?? name,
            name ?? id,
            typeColumn is null ? null : $"{QuoteSqlIdentifier(typeColumn)} = '1'",
            "leverantörer",
            "Leverantörsregister");
    }

    private static SimpleEntityListFallback? BuildEntityListSql(
        string table,
        IEnumerable<(string? Column, string Alias)> requestedColumns,
        string? requiredColumn,
        string? orderColumn,
        string? additionalFilter,
        string pluralLabel,
        string metricLabel)
    {
        var columns = requestedColumns
            .Where(item => !string.IsNullOrWhiteSpace(item.Column))
            .DistinctBy(item => item.Column, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (columns.Count == 0 || string.IsNullOrWhiteSpace(requiredColumn))
            return null;

        var predicates = new List<string>
        {
            $"{QuoteSqlIdentifier(requiredColumn)} IS NOT NULL"
        };
        if (!string.IsNullOrWhiteSpace(additionalFilter))
            predicates.Add(additionalFilter);

        var select = string.Join(
            ",\n    ",
            columns.Select(item =>
                $"{QuoteSqlIdentifier(item.Column!)} AS {QuoteSqlIdentifier(item.Alias)}"));
        var orderBy = string.IsNullOrWhiteSpace(orderColumn)
            ? QuoteSqlIdentifier(requiredColumn)
            : QuoteSqlIdentifier(orderColumn);
        var sql = $"""
            SELECT TOP (200)
                {select}
            FROM {QuoteSqlTable(table)}
            WHERE {string.Join("\n  AND ", predicates)}
            ORDER BY {orderBy}
            """;

        return new SimpleEntityListFallback(sql, pluralLabel, metricLabel);
    }

    private static Dictionary<string, List<string>> ParseDetailedSchemaTables(string schemaText)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in schemaText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var match = Regex.Match(line, @"^-\s+(?<table>[^\s(]+)\s+\((?<columns>.*)\)$");
            if (!match.Success)
                continue;

            var columns = match.Groups["columns"].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParseDetailedColumnName)
                .Where(column => column.Length > 0)
                .ToList();
            if (columns.Count > 0)
                result[match.Groups["table"].Value] = columns;
        }

        return result;
    }

    private static string ParseDetailedColumnName(string definition)
    {
        var trimmed = definition.Trim();
        var typeMatch = SchemaColumnType.Match(trimmed);
        return typeMatch.Success
            ? trimmed[..typeMatch.Index].Trim().Trim('[', ']')
            : trimmed.Split(' ', 2)[0].Trim('[', ']');
    }

    private static string? PickEntityTable(
        Dictionary<string, List<string>> tables,
        params string[] preferences)
    {
        foreach (var preference in preferences)
        {
            var exact = tables.Keys.FirstOrDefault(table =>
                table.Equals(preference, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
                return exact;
        }

        foreach (var preference in preferences)
        {
            var partial = tables.Keys.FirstOrDefault(table =>
                table.Contains(preference, StringComparison.OrdinalIgnoreCase));
            if (partial is not null)
                return partial;
        }

        return null;
    }

    private static string? FindEntityColumn(List<string> columns, params string[] hints)
    {
        foreach (var hint in hints)
        {
            var normalizedHint = NormalizeEntityIdentifier(hint);
            var exact = columns.FirstOrDefault(column =>
                NormalizeEntityIdentifier(column).Equals(normalizedHint, StringComparison.Ordinal));
            if (exact is not null)
                return exact;
        }

        return null;
    }

    private static string NormalizeEntityIdentifier(string value) =>
        Regex.Replace(value ?? string.Empty, @"[^a-zA-Z0-9åäöÅÄÖ]", string.Empty)
            .ToLowerInvariant();

    private static string QuoteSqlIdentifier(string identifier) =>
        $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static string QuoteSqlTable(string table) =>
        string.Join(
            ".",
            table.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(QuoteSqlIdentifier));

    private enum SimpleEntity
    {
        Customer,
        Product,
        Supplier
    }

    private sealed record SimpleEntityListFallback(
        string Sql,
        string PluralLabel,
        string MetricLabel);

    private static List<string> BuildMonthlyRevenueFallbackSqlCandidates(string question, string? schemaText)
    {
        var results = new List<string>();
        if (!LooksLikeMonthlyRevenueQuestion(question) || string.IsNullOrWhiteSpace(schemaText))
            return results;

        var tables = ParseSchemaTables(schemaText);
        var factCandidates = FindTablesWithColumns(
                tables,
                ["invoice date", "invoicedate", "invoice_date", "faktdat", "date"],
                ["invoice row sum", "row amount currency", "faktradsumma", "vb_faktradsumma", "bestvalue", "linetotal", "salesamount", "revenue", "amount", "belopp"])
            .OrderBy(table => table.Equals("dbo.q_zu_bi_fsg", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(ScoreFactTable)
            .ThenBy(table => table, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var factTable in factCandidates)
        {
            var columns = tables[factTable];
            var dateColumn = FindColumnName(columns, ["invoice date", "invoicedate", "invoice_date", "faktdat", "date"]);
            var revenueColumn = FindColumnName(columns, ["invoice row sum", "row amount currency", "faktradsumma", "vb_faktradsumma", "bestvalue", "linetotal", "salesamount", "revenue", "amount", "belopp"]);
            if (string.IsNullOrWhiteSpace(dateColumn) || string.IsNullOrWhiteSpace(revenueColumn))
                continue;

            var periodFilter = BuildFallbackPeriodFilter(question, "sf", dateColumn);
            if (!periodFilter.Supported)
                continue;

            var date = QualifySqlColumn("sf", dateColumn);
            var revenue = QualifySqlColumn("sf", revenueColumn);
            var where = new List<string> { $"{date} IS NOT NULL" };
            if (!string.IsNullOrWhiteSpace(periodFilter.Predicate))
                where.Add(periodFilter.Predicate);

            results.Add($@"
SELECT
    CONVERT(char(7), {date}, 120) AS [Month],
    SUM(CAST({revenue} AS decimal(18,2))) AS [TotalOmsatt]
FROM {QuoteSqlTable(factTable)} sf
WHERE {string.Join("\n  AND ", where)}
GROUP BY CONVERT(char(7), {date}, 120)
ORDER BY [Month]");
        }

        return results;
    }

    private static List<string> BuildYearToDateRevenueComparisonSqlCandidates(string question, string? schemaText)
    {
        var results = new List<string>();
        if (!LooksLikeYearToDateRevenueComparisonQuestion(question) || string.IsNullOrWhiteSpace(schemaText))
            return results;

        var tables = ParseSchemaTables(schemaText);
        var candidates = FindTablesWithColumns(tables,
                ["invoice date", "invoicedate", "invoice_date", "faktdat", "date"],
                ["invoice row sum", "row amount currency", "faktradsumma", "vb_faktradsumma", "bestvalue", "linetotal", "salesamount", "revenue", "amount", "belopp"])
            .OrderBy(table => table.Equals("dbo.q_zu_bi_fsg", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(ScoreFactTable);

        foreach (var table in candidates)
        {
            var columns = tables[table];
            var dateColumn = FindColumnName(columns, ["invoice date", "invoicedate", "invoice_date", "faktdat", "date"]);
            var revenueColumn = FindColumnName(columns, ["invoice row sum", "row amount currency", "faktradsumma", "vb_faktradsumma", "bestvalue", "linetotal", "salesamount", "revenue", "amount", "belopp"]);
            if (dateColumn is null || revenueColumn is null) continue;
            var date = QualifySqlColumn("sf", dateColumn);
            var revenue = QualifySqlColumn("sf", revenueColumn);
            results.Add($@"
SELECT
    SUM(CASE WHEN {date} >= DATEFROMPARTS(YEAR(GETDATE()), 1, 1) AND {date} < DATEADD(day, 1, CAST(GETDATE() AS date)) THEN CAST({revenue} AS decimal(18,2)) ELSE 0 END) AS [CurrentYearToDate],
    SUM(CASE WHEN {date} >= DATEFROMPARTS(YEAR(GETDATE()) - 1, 1, 1) AND {date} < DATEADD(year, -1, DATEADD(day, 1, CAST(GETDATE() AS date))) THEN CAST({revenue} AS decimal(18,2)) ELSE 0 END) AS [PreviousYearToDate],
    SUM(CASE WHEN {date} >= DATEFROMPARTS(YEAR(GETDATE()), 1, 1) AND {date} < DATEADD(day, 1, CAST(GETDATE() AS date)) THEN CAST({revenue} AS decimal(18,2)) ELSE 0 END) -
    SUM(CASE WHEN {date} >= DATEFROMPARTS(YEAR(GETDATE()) - 1, 1, 1) AND {date} < DATEADD(year, -1, DATEADD(day, 1, CAST(GETDATE() AS date))) THEN CAST({revenue} AS decimal(18,2)) ELSE 0 END) AS [Difference]
FROM {QuoteSqlTable(table)} sf");
        }
        return results;
    }

    private static List<string> BuildYearToDateRevenueSqlCandidates(string question, string? schemaText)
    {
        var results = new List<string>();
        if (!LooksLikeCurrentYearRevenueQuestion(question) || string.IsNullOrWhiteSpace(schemaText))
            return results;

        var tables = ParseSchemaTables(schemaText);
        var candidates = FindTablesWithColumns(
                tables,
                ["invoice date", "invoicedate", "invoice_date", "faktdat", "date"],
                ["invoice row sum", "row amount currency", "faktradsumma", "vb_faktradsumma", "bestvalue", "linetotal", "salesamount", "revenue", "amount", "belopp"])
            .OrderBy(table => table.Equals("dbo.q_zu_bi_fsg", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(ScoreFactTable);

        foreach (var table in candidates)
        {
            var columns = tables[table];
            var dateColumn = FindColumnName(columns, ["invoice date", "invoicedate", "invoice_date", "faktdat", "date"]);
            var revenueColumn = FindColumnName(columns, ["invoice row sum", "row amount currency", "faktradsumma", "vb_faktradsumma", "bestvalue", "linetotal", "salesamount", "revenue", "amount", "belopp"]);
            if (dateColumn is null || revenueColumn is null)
                continue;

            var date = QualifySqlColumn("sf", dateColumn);
            var revenue = QualifySqlColumn("sf", revenueColumn);
            results.Add($@"
SELECT
    SUM(CASE WHEN {date} >= DATEFROMPARTS(YEAR(GETDATE()), 1, 1)
              AND {date} < DATEADD(day, 1, CAST(GETDATE() AS date))
             THEN CAST({revenue} AS decimal(18,2)) ELSE 0 END) AS [TotalOmsatt]
FROM {QuoteSqlTable(table)} sf");
        }

        return results;
    }

    private static List<string> BuildTopCustomersFallbackSqlCandidates(string question, string? schemaText)
    {
        var results = new List<string>();

        if (string.IsNullOrWhiteSpace(question))
            return results;

        if (!LooksLikeCustomerAggregateQuestion(question))
            return results;

        var isRanking = LooksLikeTopCustomersQuestion(question);
        var topN = isRanking ? ExtractTopNFromQuestion(question) : null;
        if (isRanking && !HasExplicitTopNumber(question))
            topN = 3;
        topN = isRanking ? Math.Clamp(topN ?? 3, 1, 200) : null;
        var amountThreshold = ExtractCustomerAmountThreshold(question);

        var wantsQuantity = Regex.IsMatch(question, @"(?is)\b(antal|qty|quantity|units|stycken|enheter|sålda|sålt|orderqty)\b");
        var wantsOrderValue = Regex.IsMatch(question, @"(?is)\b(beställ\w*|order\w*)\b");
        var wantsRevenue = Regex.IsMatch(question, @"(?is)\b(omsätt\w*|omsatt|omsatta|intäkt|intakt|revenue|sales|belopp|kronor|kr|linetotal|amount|värde)\b");
        var prefersRevenue = !wantsQuantity && !wantsOrderValue;

        if (string.IsNullOrWhiteSpace(schemaText))
            return results;

        var tables = ParseSchemaTables(schemaText);
        if (tables.Count == 0)
            return results;

        var customerIdHints = new[] { "cu_pk", "customer no", "customerid", "customer_id", "customernumber", "customerkey", "kundnr", "kundnummer", "kundid" };
        var customerNameHints = new[] { "customer", "customername", "companyname", "name", "kundnamn", "kundnamn1" };
        var dateHints = new[] { "invoice date", "invoicedate", "invoice_date", "datum", "date" };
        var metricHints = wantsOrderValue
            ? new[] { "order row sum", "order amount", "order value", "order total", "order sum", "ordsum", "ordervarde", "ordervärde", "bestvalue", "linetotal", "salesamount", "totalamount", "amount", "belopp" }
            : prefersRevenue
            ? new[] { "invoice row sum", "row amount currency", "faktradsumma", "vb_faktradsumma", "bestvalue", "linetotal", "salesamount", "totalamount", "revenue", "amount", "belopp", "omsatt", "intakt" }
            : new[] { "orderqty", "quantity", "qty", "units", "antal", "sold" };

        var factCandidates = FindTablesWithColumns(tables, customerIdHints, metricHints);
        var orderedFacts = factCandidates
            .OrderBy(ScoreFactTable)
            .ThenBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var factSet = factCandidates.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dimCandidates = FindTablesWithColumns(tables, customerIdHints, customerNameHints)
            .Where(table => !factSet.Contains(table))
            .ToList();
        var dimTable = PickBestTable(dimCandidates, new[] { "q_zu_bi_customer", "dim_customer", "customer" });
        var dimCols = !string.IsNullOrWhiteSpace(dimTable) ? tables[dimTable] : null;
        var dimCustomerId = dimCols != null ? FindColumnName(dimCols, customerIdHints) : null;
        var dimCustomerName = dimCols != null ? FindColumnName(dimCols, customerNameHints) : null;

        var metricAlias = wantsOrderValue ? "TotalOrdervarde" : prefersRevenue ? "TotalOmsatt" : "TotalAntal";
        var metricCast = prefersRevenue || wantsOrderValue ? "decimal(18,2)" : "int";

        foreach (var factTable in orderedFacts)
        {
            var factCols = tables[factTable];
            var factCustomerId = FindColumnName(factCols, customerIdHints);
            var factMetric = FindColumnName(factCols, metricHints);
            if (string.IsNullOrWhiteSpace(factCustomerId) || string.IsNullOrWhiteSpace(factMetric))
                continue;
            var factCustomerName = FindColumnName(factCols, customerNameHints);
            var factDate = FindColumnName(factCols, dateHints);
            var periodFilter = BuildFallbackPeriodFilter(question, "sf", factDate);
            if (!periodFilter.Supported)
                continue;

            var sharedPkJoin = dimCols != null ? FindSharedPkJoinColumn(factCols, dimCols) : null;

            var selectParts = new List<string>();
            var groupByParts = new List<string>();
            var nullCheckColumn = factCustomerId;

            if (!string.IsNullOrWhiteSpace(dimTable) && !string.IsNullOrWhiteSpace(dimCustomerId) && !string.IsNullOrWhiteSpace(dimCustomerName))
            {
                selectParts.Add($"{QualifySqlColumn("dc", dimCustomerId)} AS [CustomerID]");
                selectParts.Add($"{QualifySqlColumn("dc", dimCustomerName)} AS [CustomerName]");
                groupByParts.Add(QualifySqlColumn("dc", dimCustomerId));
                groupByParts.Add(QualifySqlColumn("dc", dimCustomerName));
            }
            else if (!string.IsNullOrWhiteSpace(sharedPkJoin) && !string.IsNullOrWhiteSpace(dimTable) && !string.IsNullOrWhiteSpace(dimCustomerName))
            {
                selectParts.Add($"{QualifySqlColumn("dc", sharedPkJoin)} AS [CustomerID]");
                selectParts.Add($"{QualifySqlColumn("dc", dimCustomerName)} AS [CustomerName]");
                groupByParts.Add(QualifySqlColumn("dc", sharedPkJoin));
                groupByParts.Add(QualifySqlColumn("dc", dimCustomerName));
                nullCheckColumn = sharedPkJoin;
            }
            else if (!string.IsNullOrWhiteSpace(factCustomerName))
            {
                selectParts.Add($"{QualifySqlColumn("sf", factCustomerId)} AS [CustomerID]");
                selectParts.Add($"{QualifySqlColumn("sf", factCustomerName)} AS [CustomerName]");
                groupByParts.Add(QualifySqlColumn("sf", factCustomerId));
                groupByParts.Add(QualifySqlColumn("sf", factCustomerName));
            }
            else
            {
                selectParts.Add($"{QualifySqlColumn("sf", factCustomerId)} AS [CustomerID]");
                groupByParts.Add(QualifySqlColumn("sf", factCustomerId));
            }

            var qualifiedMetric = QualifySqlColumn("sf", factMetric);
            selectParts.Add($"SUM(CAST({qualifiedMetric} AS {metricCast})) AS [{metricAlias}]");

            var join = (!string.IsNullOrWhiteSpace(dimTable) && !string.IsNullOrWhiteSpace(sharedPkJoin))
                ? $"LEFT JOIN {QuoteSqlTable(dimTable)} dc ON {QualifySqlColumn("sf", sharedPkJoin)} = {QualifySqlColumn("dc", sharedPkJoin)}"
                : (!string.IsNullOrWhiteSpace(dimTable) && !string.IsNullOrWhiteSpace(dimCustomerId))
                    ? $"LEFT JOIN {QuoteSqlTable(dimTable)} dc ON {QualifySqlColumn("sf", factCustomerId)} = {QualifySqlColumn("dc", dimCustomerId)}"
                    : string.Empty;
            var whereParts = new List<string>
            {
                $"{QualifySqlColumn("sf", nullCheckColumn)} IS NOT NULL"
            };
            if (!string.IsNullOrWhiteSpace(periodFilter.Predicate))
                whereParts.Add(periodFilter.Predicate);

            var limitClause = topN.HasValue ? $"TOP ({topN.Value}) " : string.Empty;
            var havingClause = amountThreshold.HasValue
                ? $"HAVING SUM(CAST({qualifiedMetric} AS {metricCast})) > {amountThreshold.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                : "HAVING SUM(CAST(" + qualifiedMetric + " AS " + metricCast + ")) > 0";

            results.Add($@"
SELECT {limitClause}
    {string.Join(",\n    ", selectParts)}
FROM {QuoteSqlTable(factTable)} sf
{join}
WHERE {string.Join("\n  AND ", whereParts)}
GROUP BY {string.Join(", ", groupByParts)}
{havingClause}
ORDER BY [{metricAlias}] DESC");
        }

        return results;
    }

    private static int? ExtractTopNFromQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return null;

        var normalized = question.ToLowerInvariant();
        var swedishNumberWords = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = 1,
            ["ett"] = 1,
            ["två"] = 2,
            ["tre"] = 3,
            ["fyra"] = 4,
            ["fem"] = 5,
            ["sex"] = 6,
            ["sju"] = 7,
            ["åtta"] = 8,
            ["nio"] = 9,
            ["tio"] = 10
        };
        var wordMatch = Regex.Match(
            normalized,
            @"(?is)\b(?<n>en|ett|två|tre|fyra|fem|sex|sju|åtta|nio|tio)\s+(?:bästa|största|högsta)\b");
        if (wordMatch.Success &&
            swedishNumberWords.TryGetValue(wordMatch.Groups["n"].Value, out var wordNumber))
        {
            return wordNumber;
        }

        // Common patterns: "top 5", "topp 5", "visa 10"
        var m = Regex.Match(question, @"(?is)\btop(p)?\s+(?<n>\d{1,3})\b");
        if (m.Success && int.TryParse(m.Groups["n"].Value, out var n1))
            return Math.Clamp(n1, 1, 200);

        m = Regex.Match(question, @"(?is)\b(?<n>\d{1,3})\s+(?:bästa|bäst|best|mest|största|högsta)\b");
        if (m.Success && int.TryParse(m.Groups["n"].Value, out var n2))
            return Math.Clamp(n2, 1, 200);

        m = Regex.Match(question, @"(?is)\b(?:bästa|bäst|best|mest|största|högsta)\s+(?<n>\d{1,3})\b");
        if (m.Success && int.TryParse(m.Groups["n"].Value, out var n3))
            return Math.Clamp(n3, 1, 200);

        if (Regex.IsMatch(question, @"(?is)\b(bästa|bäst|best|mest|topp|top)\b"))
        {
            m = Regex.Match(question, @"(?is)\b(?<n>\d{1,3})\b");
            if (m.Success && int.TryParse(m.Groups["n"].Value, out var n4))
                return Math.Clamp(n4, 1, 200);
        }

        m = Regex.Match(question, @"(?is)\bvisa\s+(?<n>\d{1,3})\b");
        if (m.Success && int.TryParse(m.Groups["n"].Value, out var n5))
            return Math.Clamp(n5, 1, 200);

        // "största", "högsta", "max" typically means 1 record.
        if (Regex.IsMatch(question, @"(?is)\bstörsta\b|\bhögsta\b|\bmax\b|\bmaxim(al|alt)\b"))
            return 1;

        // "bäst", "best", "mest" typically means top 1.
        if (Regex.IsMatch(question, @"(?is)\bbäst(a|e)?\b|\bbest\b|\bmest\b"))
            return 1;

        return null;
    }

    private static decimal? ExtractCustomerAmountThreshold(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return null;

        var match = Regex.Match(
            question,
            @"(?is)\b(?:över|over|mer\s+än|more\s+than)\s+(?<amount>\d[\d\s.,]*)\s*(?:kr|sek|kronor)?\b");
        if (!match.Success)
            return null;

        var normalized = Regex.Replace(match.Groups["amount"].Value, @"\s", string.Empty)
            .Replace(",", ".", StringComparison.Ordinal);
        return decimal.TryParse(
            normalized,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var amount) && amount > 0 && amount <= 1_000_000_000m
            ? amount
            : null;
    }


    private static string? BuildTopProductsFallbackSql(string question, string? schemaText)
    {
        return BuildTopProductsFallbackSqlCandidates(question, schemaText).FirstOrDefault();
    }

    private static List<string> BuildTopProductsFallbackSqlCandidates(string question, string? schemaText)
    {
        var results = new List<string>();

        if (string.IsNullOrWhiteSpace(question))
            return results;

        if (!LooksLikeTopProductsQuestion(question))
            return results;

        var topN = ExtractTopNFromQuestion(question) ?? 1;
        topN = Math.Clamp(topN, 1, 200);

        var wantsRevenue = Regex.IsMatch(question, @"(?is)\b(omsätt\w*|omsatt|omsatta|intäkt|intakt|revenue|sales|belopp|kronor|kr|linetotal|amount|värde)\b");

        if (string.IsNullOrWhiteSpace(schemaText))
            return results;

        var tables = ParseSchemaTables(schemaText);
        if (tables.Count == 0)
            return results;

        var metricHints = wantsRevenue
            ? new[] { "invoice row sum", "row amount currency", "faktradsumma", "vb_faktradsumma", "bestvalue", "linetotal", "salesamount", "totalamount", "revenue", "amount", "belopp", "omsatt", "intakt" }
            : new[] { "orderqty", "quantity", "qty", "units", "antal", "sold" };
        var productIdHints = new[] { "ar_pk", "item no", "item", "itemid", "artnr", "article", "articleno", "productid", "product_id", "productkey", "productno", "produktid" };
        var productNameHints = new[] { "item description", "productname", "product_name", "produktnamn", "name" };
        var categoryHints = new[] { "productcategoryname", "categoryname", "kategori", "category" };
        var dateHints = new[] { "invoice date", "invoicedate", "invoice_date", "datum", "date" };

        var factCandidates = FindTablesWithColumns(tables, productIdHints, metricHints);
        var orderedFacts = factCandidates
            .OrderBy(ScoreFactTable)
            .ThenBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (orderedFacts.Count == 0)
            return results;

        var factSet = factCandidates.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dimCandidates = FindTablesWithColumns(tables, productIdHints, productNameHints)
            .Where(table => !factSet.Contains(table))
            .ToList();
        var dimTable = PickBestTable(dimCandidates, new[] { "q_zu_bi_item", "dim_product", "product" });
        var dimCols = !string.IsNullOrWhiteSpace(dimTable) ? tables[dimTable] : null;
        var dimProductId = dimCols != null ? FindColumnName(dimCols, productIdHints) : null;
        var dimProductName = dimCols != null ? FindColumnName(dimCols, productNameHints) : null;
        var dimCategory = dimCols != null ? FindColumnName(dimCols, categoryHints) : null;

        var metricAlias = wantsRevenue ? "TotalOmsatt" : "TotalAntal";
        var metricCast = wantsRevenue ? "decimal(18,2)" : "int";

        foreach (var factTable in orderedFacts)
        {
            var factCols = tables[factTable];
            var factProductId = FindColumnName(factCols, productIdHints);
            var factMetric = FindColumnName(factCols, metricHints);
            if (string.IsNullOrWhiteSpace(factProductId) || string.IsNullOrWhiteSpace(factMetric))
                continue;

            var factProductName = FindColumnName(factCols, productNameHints);
            var factDate = FindColumnName(factCols, dateHints);
            var periodFilter = BuildFallbackPeriodFilter(question, "sf", factDate);
            if (!periodFilter.Supported)
                continue;

            var sharedPkJoin = dimCols != null ? FindSharedPkJoinColumn(factCols, dimCols) : null;

            var selectParts = new List<string>();
            var groupByParts = new List<string>();
            var nullCheckColumn = factProductId;

            if (!string.IsNullOrWhiteSpace(dimTable) && !string.IsNullOrWhiteSpace(dimProductName) && !string.IsNullOrWhiteSpace(dimProductId))
            {
                selectParts.Add($"{QualifySqlColumn("dp", dimProductId)} AS [ProductID]");
                selectParts.Add($"{QualifySqlColumn("dp", dimProductName)} AS [ProductName]");
                groupByParts.Add(QualifySqlColumn("dp", dimProductId));
                groupByParts.Add(QualifySqlColumn("dp", dimProductName));

                if (!string.IsNullOrWhiteSpace(dimCategory))
                {
                    selectParts.Add($"{QualifySqlColumn("dp", dimCategory)} AS [ProductCategory]");
                    groupByParts.Add(QualifySqlColumn("dp", dimCategory));
                }
            }
            else if (!string.IsNullOrWhiteSpace(sharedPkJoin) && !string.IsNullOrWhiteSpace(dimTable) && !string.IsNullOrWhiteSpace(dimProductName))
            {
                selectParts.Add($"{QualifySqlColumn("dp", sharedPkJoin)} AS [ProductID]");
                selectParts.Add($"{QualifySqlColumn("dp", dimProductName)} AS [ProductName]");
                groupByParts.Add(QualifySqlColumn("dp", sharedPkJoin));
                groupByParts.Add(QualifySqlColumn("dp", dimProductName));
                nullCheckColumn = sharedPkJoin;

                if (!string.IsNullOrWhiteSpace(dimCategory))
                {
                    selectParts.Add($"{QualifySqlColumn("dp", dimCategory)} AS [ProductCategory]");
                    groupByParts.Add(QualifySqlColumn("dp", dimCategory));
                }
            }
            else if (!string.IsNullOrWhiteSpace(factProductName))
            {
                selectParts.Add($"{QualifySqlColumn("sf", factProductId)} AS [ProductID]");
                selectParts.Add($"{QualifySqlColumn("sf", factProductName)} AS [ProductName]");
                groupByParts.Add(QualifySqlColumn("sf", factProductId));
                groupByParts.Add(QualifySqlColumn("sf", factProductName));
            }
            else
            {
                selectParts.Add($"{QualifySqlColumn("sf", factProductId)} AS [ProductID]");
                groupByParts.Add(QualifySqlColumn("sf", factProductId));
            }

            var qualifiedMetric = QualifySqlColumn("sf", factMetric);
            selectParts.Add($"SUM(CAST({qualifiedMetric} AS {metricCast})) AS [{metricAlias}]");

            var join = (!string.IsNullOrWhiteSpace(dimTable) && !string.IsNullOrWhiteSpace(sharedPkJoin))
                ? $"LEFT JOIN {QuoteSqlTable(dimTable)} dp ON {QualifySqlColumn("sf", sharedPkJoin)} = {QualifySqlColumn("dp", sharedPkJoin)}"
                : (!string.IsNullOrWhiteSpace(dimTable) && !string.IsNullOrWhiteSpace(dimProductId))
                    ? $"LEFT JOIN {QuoteSqlTable(dimTable)} dp ON {QualifySqlColumn("sf", factProductId)} = {QualifySqlColumn("dp", dimProductId)}"
                    : string.Empty;
            var whereParts = new List<string>
            {
                $"{QualifySqlColumn("sf", nullCheckColumn)} IS NOT NULL"
            };
            if (!string.IsNullOrWhiteSpace(periodFilter.Predicate))
                whereParts.Add(periodFilter.Predicate);

            results.Add($@"
SELECT TOP ({topN})
    {string.Join(",\n    ", selectParts)}
FROM {QuoteSqlTable(factTable)} sf
{join}
WHERE {string.Join("\n  AND ", whereParts)}
GROUP BY {string.Join(", ", groupByParts)}
HAVING SUM(CAST({qualifiedMetric} AS {metricCast})) > 0
ORDER BY [{metricAlias}] DESC");
        }

        return results;
    }

    private static int ScoreFactTable(string table)
    {
        var lower = (table ?? string.Empty).ToLowerInvariant();
        if (lower.Contains("q_zu_bi_fsg"))
            return 0;
        if (lower.Contains("salesfact"))
            return 1;
        if (lower.Contains("fact"))
            return 2;
        if (lower.Contains("sales"))
            return 3;
        if (lower.Contains("order"))
            return 4;
        if (lower.Contains("import"))
            return 5;
        return 10;
    }

    private static Dictionary<string, List<string>> ParseSchemaTables(string schemaText)
    {
        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in schemaText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (!line.StartsWith("- ", StringComparison.Ordinal))
                continue;

            var afterDash = line.Substring(2).Trim();
            var parenIdx = afterDash.IndexOf(" (", StringComparison.Ordinal);
            if (parenIdx < 0)
                continue;

            var table = afterDash.Substring(0, parenIdx).Trim();
            var colsPart = afterDash.Substring(parenIdx).Trim();
            if (!colsPart.StartsWith("(", StringComparison.Ordinal) || !colsPart.EndsWith(")", StringComparison.Ordinal))
                continue;

            var colsRaw = colsPart.Trim('(', ')');
            var cols = colsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Select(ParseDetailedColumnName)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            if (cols.Count > 0)
                dict[table] = cols;
        }

        return dict;
    }

    private static List<string> FindTablesWithColumns(Dictionary<string, List<string>> tables, string[] firstHints, string[] secondHints)
    {
        var matches = new List<string>();
        foreach (var kv in tables)
        {
            var cols = kv.Value;
            if (FindColumnName(cols, firstHints) is null)
                continue;
            if (FindColumnName(cols, secondHints) is null)
                continue;
            matches.Add(kv.Key);
        }

        return matches;
    }

    private static string? PickBestTable(List<string> candidates, string[] preferredHints)
    {
        if (candidates.Count == 0)
            return null;

        int Score(string table)
        {
            var lower = table.ToLowerInvariant();
            for (var i = 0; i < preferredHints.Length; i++)
            {
                if (lower.Contains(preferredHints[i]))
                    return i;
            }
            return preferredHints.Length + 1;
        }

        return candidates
            .OrderBy(Score)
            .ThenBy(t => t, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static string? FindColumnName(List<string> columns, string[] hints)
    {
        var normalizedColumns = columns
            .Select(column => new
            {
                Column = column,
                Normalized = NormalizeEntityIdentifier(column)
            })
            .ToList();
        var normalizedHints = hints
            .Select(NormalizeEntityIdentifier)
            .Where(hint => hint.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var hint in normalizedHints)
        {
            var match = normalizedColumns.FirstOrDefault(column =>
                column.Normalized.Equals(hint, StringComparison.Ordinal));
            if (match is not null)
                return match.Column;
        }

        foreach (var hint in normalizedHints)
        {
            var match = normalizedColumns.FirstOrDefault(column =>
                column.Normalized.Contains(hint, StringComparison.Ordinal));
            if (match is not null)
                return match.Column;
        }

        return null;
    }

    private static string QualifySqlColumn(string alias, string column) =>
        $"{alias}.{QuoteSqlIdentifier(column)}";

    private static FallbackPeriodFilter BuildFallbackPeriodFilter(
        string question,
        string tableAlias,
        string? dateColumn)
    {
        var hasMonthlyBreakdownHint = Regex.IsMatch(
            question ?? string.Empty,
            @"(?is)\b(per\s+månad|månadsvis|monthly)\b");
        if (!HasPeriodHint(question ?? string.Empty) && !hasMonthlyBreakdownHint)
            return new FallbackPeriodFilter(true, null);

        if (string.IsNullOrWhiteSpace(dateColumn))
            return new FallbackPeriodFilter(false, null);

        var normalized = (question ?? string.Empty).ToLowerInvariant();
        var qualifiedDate = QualifySqlColumn(tableAlias, dateColumn);
        if (Regex.IsMatch(normalized, @"(?is)\b(i\s+år|detta\s+år|innevarande\s+år|this\s+year)\b") ||
            hasMonthlyBreakdownHint)
        {
            return new FallbackPeriodFilter(
                true,
                $"{qualifiedDate} >= DATEFROMPARTS(YEAR(GETDATE()), 1, 1) AND {qualifiedDate} < DATEADD(day, 1, CAST(GETDATE() AS date))");
        }

        return new FallbackPeriodFilter(false, null);
    }

    private sealed record FallbackPeriodFilter(bool Supported, string? Predicate);

    private static string? FindSharedPkJoinColumn(List<string> leftColumns, List<string> rightColumns)
    {
        if (leftColumns.Count == 0 || rightColumns.Count == 0)
            return null;

        var rightSet = new HashSet<string>(rightColumns, StringComparer.OrdinalIgnoreCase);
        var shared = leftColumns
            .Where(c => rightSet.Contains(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (shared.Count == 0)
            return null;

        var preferred = new[] { "ar_pk", "cu_pk", "customer_pk", "item_pk", "article_pk", "product_pk" };
        foreach (var key in preferred)
        {
            var hit = shared.FirstOrDefault(c => c.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(hit))
                return hit;
        }

        var genericPk = shared.FirstOrDefault(c => c.EndsWith("_pk", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(genericPk))
            return genericPk;

        return null;
    }
    private static bool LooksLikeFactQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return false;
        var q = question.ToLowerInvariant();

        // Swedish/English keywords that usually require transactional/fact tables.
        var needles = new[]
        {
            "omsättning", "intäkt", "försälj", "revenue", "sales",
            "order", "ordrar", "orders",
            "faktura", "faktur", "invoice",
            "obetal", "förfall", "öppen", "öppna", "open", "overdue", "unpaid",
            "snittordervärde", "aov",
            "kpi", "trend", "toppsälj", "top seller", "bruttomarginal", "margin",
            // Common Swedish phrasing for "top seller" questions
            "bästsälj", "bäst sälj", "mest såld", "mest sålda", "säljer vi mest", "sålt mest", "top produkt", "topp produkt"
        };

        return needles.Any(q.Contains);
    }

    private string ExpandClarificationQuestion(string question, string dataSourceKey, int? companyCode)
    {
        var trimmed = (question ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return string.Empty;

        if (!IsMetricOnlyAnswer(trimmed))
            return trimmed;

        var pending = GetPendingMetricQuestion(dataSourceKey, companyCode);
        if (string.IsNullOrWhiteSpace(pending))
            return trimmed;

        ClearPendingMetricQuestion(dataSourceKey, companyCode);
        return $"{pending} ({trimmed})";
    }

    private string ExpandBreakdownFollowUpQuestion(string question, string memoryKey)
    {
        var trimmed = (question ?? string.Empty).Trim();
        if (!LooksLikeMonthlyBreakdownFollowUp(trimmed))
            return trimmed;

        var context = _memory.GetLastResultContext(memoryKey);
        if (context is null)
            return trimmed;

        var metric = context.Metric?.Trim().ToLowerInvariant() switch
        {
            "net_revenue" => "omsättning",
            "quantity_sold" => "antal",
            "order_value" => "ordervärde",
            "invoice_balance" => "utestående fakturabelopp",
            _ => null
        };

        if (string.IsNullOrWhiteSpace(metric))
            return trimmed;

        var period = context.Period?.Trim().ToLowerInvariant() switch
        {
            "current_year" => " i år",
            "previous_year" => " förra året",
            "last_12_months" => " de senaste 12 månaderna",
            "current_month" => " i månaden",
            _ => string.Empty
        };

        return $"Visa {metric} per månad{period}";
    }

    private static bool LooksLikeMonthlyBreakdownFollowUp(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return false;

        return Regex.IsMatch(
            question,
            @"(?is)\b(bryt\s*(ned|ner)|fördela|uppdelning)\b.*\b(per\s+månad|månadsvis|månad)\b");
    }

    private static bool IsMetricOnlyAnswer(string question)
    {
        return Regex.IsMatch(question, @"(?is)^\s*(antal|omsättning|omsatt|omsatta|intäkt|intakt|revenue)\s*$");
    }

    private string GetPendingMetricQuestion(string dataSourceKey, int? companyCode)
    {
        var session = _http.HttpContext?.Session;
        if (session == null)
            return string.Empty;

        var key = BuildPendingMetricQuestionKey(dataSourceKey, companyCode);
        return session.GetString(key) ?? string.Empty;
    }

    private void SetPendingMetricQuestion(string dataSourceKey, int? companyCode, string question)
    {
        var session = _http.HttpContext?.Session;
        if (session == null)
            return;

        var key = BuildPendingMetricQuestionKey(dataSourceKey, companyCode);
        session.SetString(key, question ?? string.Empty);
    }

    private void ClearPendingMetricQuestion(string dataSourceKey, int? companyCode)
    {
        var session = _http.HttpContext?.Session;
        if (session == null)
            return;

        var key = BuildPendingMetricQuestionKey(dataSourceKey, companyCode);
        session.Remove(key);
    }

    private static string BuildPendingMetricQuestionKey(string dataSourceKey, int? companyCode)
    {
        var ds = (dataSourceKey ?? "default").Trim();
        var companySegment = companyCode.HasValue ? $"c{companyCode.Value}" : "c-none";
        return $"{PendingMetricQuestionSessionPrefix}{ds}:{companySegment}";
    }

    private static bool ShouldAskMetricClarification(string question, string? schemaText)
    {
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(schemaText))
            return false;

        var q = question.ToLowerInvariant();
        var hasTopIntent = Regex.IsMatch(q, @"(?is)\b(top(p)?|topp|bäst(a|e)?|best|mest|största|högsta)\b");
        if (!hasTopIntent)
            return false;

        var hasProductIntent = Regex.IsMatch(
            q,
            @"(?is)\b(produkt(?:en|er|erna)?|product(?:s)?|artikel(?:n)?|artiklar(?:na)?|vara(?:n|or|orna)?)\b");
        if (!hasProductIntent)
            return false;

        var hasQuantityIntent = Regex.IsMatch(q, @"(?is)\b(antal|qty|quantity|units|stycken|enheter|sålda|sålt|orderqty)\b");
        var hasRevenueIntent = Regex.IsMatch(q, @"(?is)\b(omsätt\w*|omsatt|omsatta|intäkt|intakt|revenue|sales|belopp|kronor|kr|linetotal|amount|värde)\b");
        if (hasQuantityIntent || hasRevenueIntent)
            return false;

        var hasQtyColumn = Regex.IsMatch(schemaText, @"(?is)\b(orderqty|quantity|qty|units|antal)\b");
        var hasRevenueColumn = Regex.IsMatch(
            schemaText,
            @"(?is)\b(invoice\s+row\s+sum|row\s+amount\s+currency|linetotal|totalamount|salesamount|revenue|amount|belopp|omsatt|intakt|värde)\b");

        return hasQtyColumn && hasRevenueColumn;
    }

    private static bool LooksLikeTopProductsQuestion(string question, string? sql = null)
    {
        if (!string.IsNullOrWhiteSpace(question))
        {
            var q = question.ToLowerInvariant();
            var hasTop = Regex.IsMatch(q, @"(?is)\b(top(p)?|topp|bäst(a|e)?|best|mest|största|högsta)\b");
            var hasProduct = Regex.IsMatch(
                q,
                @"(?is)\b(produkt(?:en|er|erna)?|product(?:s)?|artikel(?:n)?|artiklar(?:na)?|vara(?:n|or|orna)?)\b");
            if (hasTop && hasProduct)
                return true;
        }

        if (!string.IsNullOrWhiteSpace(sql))
        {
            var s = sql.ToLowerInvariant();
            return s.Contains("product") && s.Contains("sum(") && s.Contains("group by");
        }

        return false;
    }

    private static bool LooksLikeTopCustomersQuestion(string question, string? sql = null)
    {
        if (!string.IsNullOrWhiteSpace(question))
        {
            var q = question.ToLowerInvariant();
            var hasTop = Regex.IsMatch(q, @"(?is)\b(top(p)?|topp|bäst(a|e)?|best|mest|största|högsta)\b");
            var hasCustomer = Regex.IsMatch(
                q,
                @"(?is)\b(kund(?:en|er|erna)?|customer(?:s)?|klient(?:en|er|erna)?|client(?:s)?)\b");
            if (hasTop && hasCustomer)
                return true;
        }

        if (!string.IsNullOrWhiteSpace(sql))
        {
            var s = sql.ToLowerInvariant();
            return s.Contains("customer") && s.Contains("sum(") && s.Contains("group by");
        }

        return false;
    }

    private static bool LooksLikeCustomerAggregateQuestion(string question) =>
        LooksLikeTopCustomersQuestion(question) || LooksLikeCustomerAmountThresholdQuestion(question);

    // Keeps common, unambiguous questions out of the costly schema-to-SQL model path.
    // A failed template deliberately falls through to the generative planner.
    private static bool ShouldUseDeterministicFastPath(string question)
    {
        var normalizedQuestion = question ?? string.Empty;
        var hasAdvancedComparison = Regex.IsMatch(
            normalizedQuestion,
            @"(?is)\b(jämför|jämfört|skillnad|utveckling|ökat|minskat|fallit|förändr\w*)\b");

        return ClassifySimpleEntityList(normalizedQuestion) is not null ||
               (LooksLikeCustomerAggregateQuestion(normalizedQuestion) && !hasAdvancedComparison) ||
               LooksLikeMonthlyRevenueQuestion(normalizedQuestion) ||
               LooksLikeYearToDateRevenueComparisonQuestion(normalizedQuestion) ||
               LooksLikeCurrentYearRevenueQuestion(normalizedQuestion) ||
               (LooksLikeTopProductsQuestion(normalizedQuestion) && !hasAdvancedComparison);
    }

    private static bool LooksLikeMonthlyRevenueQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return false;

        var normalized = question.ToLowerInvariant();
        var hasRevenue = Regex.IsMatch(normalized, @"(?is)\b(omsätt\w*|omsatt\w*|intäkt|revenue|sales|försälj\w*)\b");
        var hasMonth = Regex.IsMatch(normalized, @"(?is)\b(per\s+månad|månadsvis|månad)\b");
        return hasRevenue && hasMonth;
    }

    private static bool LooksLikeYearToDateRevenueComparisonQuestion(string question)
    {
        var q = question?.ToLowerInvariant() ?? string.Empty;
        var hasComparisonPeriod = Regex.IsMatch(q, @"(?is)\b(förra\s+årets?|föregående\s+års?|i\s+fjol|fjolårets?|previous\s+year|last\s+year)\b") &&
                                  Regex.IsMatch(q, @"(?is)\b(jämför|jämfört|mot|samma\s+månader|samma\s+period)\b");
        var hasRevenueIntent = Regex.IsMatch(q, @"(?is)\b(omsätt\w*|omsatt\w*|intäkt|revenue|sales|försälj\w*)\b");
        var hasImplicitRevenueIntent = Regex.IsMatch(q, @"(?is)\b(ytd|hittills\s+i\s+år|hur\s+ligger\s+vi\s+till)\b");
        var hasBreakdown = Regex.IsMatch(q, @"(?is)\bper\s+(kund\w*|customer\w*|säljare|seller\w*|artikel\w*|produkt\w*|project\w*|projekt\w*|månad\w*)\b");
        var hasEntityComparison = Regex.IsMatch(q, @"(?is)\b(kund\w*|customer\w*|säljare|seller\w*|artikel\w*|produkt\w*|project\w*|projekt\w*)\b");
        return hasComparisonPeriod && !hasBreakdown && !hasEntityComparison && (hasRevenueIntent || hasImplicitRevenueIntent);
    }

    private static bool LooksLikeCurrentYearRevenueQuestion(string question)
    {
        var q = question?.ToLowerInvariant() ?? string.Empty;
        return Regex.IsMatch(q, @"(?is)\b(omsätt\w*|omsatt\w*|intäkt|revenue|sales|försälj\w*)\b") &&
               Regex.IsMatch(q, @"(?is)\b(i\s+års?|detta\s+års?|innevarande\s+års?|årets|current\s+year)\b") &&
               !LooksLikeYearToDateRevenueComparisonQuestion(q) &&
               !LooksLikeMonthlyRevenueQuestion(q);
    }

    private static bool LooksLikeCustomerAmountThresholdQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question) || !ExtractCustomerAmountThreshold(question).HasValue)
            return false;

        var q = question.ToLowerInvariant();
        var hasCustomer = Regex.IsMatch(q, @"(?is)\b(kund(?:en|er|erna)?|customer(?:s)?|klient(?:en|er|erna)?|client(?:s)?)\b");
        var hasTransaction = Regex.IsMatch(q, @"(?is)\b(beställ\w*|order\w*|omsätt\w*|omsatt\w*|intäkt|revenue|sales|försälj\w*)\b");
        return hasCustomer && hasTransaction;
    }

    private static bool HasExplicitTopNumber(string question)
    {
        return !string.IsNullOrWhiteSpace(question) &&
               Regex.IsMatch(
                   question,
                   @"(?is)\b(?:\d{1,3}|en|ett|två|tre|fyra|fem|sex|sju|åtta|nio|tio)\b");
    }

    private static bool HasCustomerMetricIntent(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return false;

        return Regex.IsMatch(question, @"(?is)\b(antal|qty|quantity|units|stycken|enheter|sålda|sålt|orderqty|omsätt\w*|omsatt|omsatta|intäkt|intakt|revenue|sales|belopp|kronor|kr|linetotal|amount|värde|lönsam|profit|margin|marginal|täckningsbidrag)\b");
    }

    private static bool HasPeriodHint(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return false;

        return Regex.IsMatch(question, @"(?is)\b(senaste|förra|föregående|denna|den här|hittills|månad|månader|år|veck(a|or)|dag(ar)?|period|datum|från|till|between|from|to|last)\b");
    }

    private static bool WantsExpandedAnswer(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return false;

        return Regex.IsMatch(question, @"(?is)\b(analysera|analys|tolka|tolkning|insikt|insikter|rekommendera|rekommendation|nästa steg|förklara|varför|orsak|drivare|jämför|jämförelse|trend|utveckling)\b");
    }

    private static string AppendCustomerFollowUpIfNeeded(string answer, string question, bool isDashboard)
    {
        if (isDashboard || string.IsNullOrWhiteSpace(answer))
            return answer;

        if (!LooksLikeTopCustomersQuestion(question))
            return answer;

        if (HasCustomerMetricIntent(question) || HasPeriodHint(question))
            return answer;

        return $"{answer}\n\nVill du se kunderna efter lönsamhet, antal, eller för en viss period (t.ex. senaste 30 dagar)?";
    }
}

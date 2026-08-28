using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Repository.Execution;
using WebApp.Models.Invoices;

namespace WebApp.Repositories.Invoices
{
    public class BiInvoicesRepository : IBiInvoicesRepository
    {
        private const string GetAllInvoicesSql = @"
WITH grouped AS (
    SELECT
        CAST([Invoice No] AS varchar(50)) AS InvoiceNo,
        MAX(COALESCE([Customer No], '')) AS Customer,
        MAX(COALESCE([Sales Code], '')) AS SalesPerson,
        MAX([Invoice Date]) AS InvoiceDate,
        CAST(SUM(COALESCE(CAST([Invoice Row SUM] AS decimal(18,2)), 0)) AS decimal(18,2)) AS AmountInclVat,
        MAX(CAST([Company] AS varchar(50))) AS CompanyCode
    FROM [dbo].[q_zu_bi_fsg]
    WHERE (@CompanyCode IS NULL OR [Company] = @CompanyCode)
      AND (@Search IS NULL OR CAST([Invoice No] AS varchar(50)) LIKE @Search OR COALESCE([Customer No], '') LIKE @Search OR COALESCE([Sales Code], '') LIKE @Search)
      AND (@FromDate IS NULL OR [Invoice Date] >= @FromDate)
      AND (@ToDate IS NULL OR [Invoice Date] <= @ToDate)
    GROUP BY [Invoice No]
)
SELECT
    InvoiceNo,
    Customer,
    SalesPerson,
    InvoiceDate,
    InvoiceDate AS DueDate,
    InvoiceDate AS PaidDate,
    AmountInclVat,
    AmountInclVat AS AmountExclVat,
    AmountInclVat AS PaidAmount,
    CAST(0 AS decimal(18,2)) AS RemainingAmount,
    CAST('' AS varchar(50)) AS Ocr,
    CompanyCode
FROM grouped
ORDER BY InvoiceDate DESC, TRY_CAST(InvoiceNo AS bigint) DESC;";

        private const string GetInvoicesPageSql = @"
WITH grouped AS (
    SELECT
        CAST([Invoice No] AS varchar(50)) AS InvoiceNo,
        MAX(COALESCE([Customer No], '')) AS Customer,
        MAX(COALESCE([Sales Code], '')) AS SalesPerson,
        MAX([Invoice Date]) AS InvoiceDate,
        CAST(SUM(COALESCE(CAST([Invoice Row SUM] AS decimal(18,2)), 0)) AS decimal(18,2)) AS AmountInclVat,
        MAX(CAST([Company] AS varchar(50))) AS CompanyCode
    FROM [dbo].[q_zu_bi_fsg]
    WHERE (@CompanyCode IS NULL OR [Company] = @CompanyCode)
      AND (@Search IS NULL OR CAST([Invoice No] AS varchar(50)) LIKE @Search OR COALESCE([Customer No], '') LIKE @Search OR COALESCE([Sales Code], '') LIKE @Search)
      AND (@FromDate IS NULL OR [Invoice Date] >= @FromDate)
      AND (@ToDate IS NULL OR [Invoice Date] <= @ToDate)
    GROUP BY [Invoice No]
)
SELECT *
INTO #GroupedInvoicesBi
FROM grouped;

SELECT
    CAST(0 AS decimal(18,2)) AS TotalUnpaidSek,
    CAST(SUM(AmountInclVat) AS decimal(18,2)) AS TotalPaidSek,
    CAST(0 AS int) AS UnpaidCount,
    COUNT(1) AS PaidCount,
    CAST(0 AS int) AS OverdueCount
FROM #GroupedInvoicesBi;

SELECT COUNT(1) AS TotalCount
FROM #GroupedInvoicesBi;

WITH numbered AS (
    SELECT
        InvoiceNo,
        Customer,
        SalesPerson,
        InvoiceDate,
        InvoiceDate AS DueDate,
        InvoiceDate AS PaidDate,
        AmountInclVat,
        AmountInclVat AS AmountExclVat,
        AmountInclVat AS PaidAmount,
        CAST(0 AS decimal(18,2)) AS RemainingAmount,
        CAST('' AS varchar(50)) AS Ocr,
        CompanyCode,
        ROW_NUMBER() OVER (ORDER BY InvoiceDate DESC, TRY_CAST(InvoiceNo AS bigint) DESC) AS RowNum
    FROM #GroupedInvoicesBi
)
SELECT *
FROM numbered
WHERE RowNum BETWEEN @StartRow AND @EndRow
ORDER BY RowNum;

DROP TABLE #GroupedInvoicesBi;";

        private const string GetInvoiceSql = @"
SELECT TOP (1)
    InvoiceNo,
    Customer,
    SalesPerson,
    InvoiceDate,
    InvoiceDate AS DueDate,
    InvoiceDate AS PaidDate,
    AmountInclVat,
    AmountInclVat AS AmountExclVat,
    AmountInclVat AS PaidAmount,
    CAST(0 AS decimal(18,2)) AS RemainingAmount,
    CAST('' AS varchar(50)) AS Ocr,
    CompanyCode
FROM (
    SELECT
        CAST([Invoice No] AS varchar(50)) AS InvoiceNo,
        MAX(COALESCE([Customer No], '')) AS Customer,
        MAX(COALESCE([Sales Code], '')) AS SalesPerson,
        MAX([Invoice Date]) AS InvoiceDate,
        CAST(SUM(COALESCE(CAST([Invoice Row SUM] AS decimal(18,2)), 0)) AS decimal(18,2)) AS AmountInclVat,
        MAX(CAST([Company] AS varchar(50))) AS CompanyCode
    FROM [dbo].[q_zu_bi_fsg]
    WHERE CAST([Invoice No] AS varchar(50)) = @InvoiceNo
      AND (@CompanyCode IS NULL OR [Company] = @CompanyCode)
    GROUP BY [Invoice No]
) grouped
ORDER BY InvoiceDate DESC;";

        private const string GetLatestInvoiceDateSql = @"
SELECT MAX([Invoice Date])
FROM [dbo].[q_zu_bi_fsg]
WHERE (@CompanyCode IS NULL OR [Company] = @CompanyCode);";

        private const string GetDashboardSummarySql = @"
WITH grouped AS (
    SELECT
        CAST([Invoice No] AS varchar(50)) AS InvoiceNo,
        MAX(COALESCE([Customer No], '')) AS Customer,
        MAX(COALESCE([Sales Code], '')) AS SalesPerson,
        MAX([Invoice Date]) AS InvoiceDate,
        CAST(SUM(COALESCE(CAST([Invoice Row SUM] AS decimal(18,2)), 0)) AS decimal(18,2)) AS AmountInclVat,
        MAX(CAST([Company] AS varchar(50))) AS CompanyCode
    FROM [dbo].[q_zu_bi_fsg]
    WHERE (@CompanyCode IS NULL OR [Company] = @CompanyCode)
    GROUP BY [Invoice No]
)
SELECT
    CAST(0 AS decimal(18,2)) AS TotalUnpaidSek,
    CAST(SUM(AmountInclVat) AS decimal(18,2)) AS TotalPaidSek,
    CAST(0 AS int) AS UnpaidCount
FROM grouped;";

        private readonly IJeevesSqlExecutor _jeevesSqlExecutor;

        public BiInvoicesRepository(IJeevesSqlExecutor jeevesSqlExecutor)
        {
            _jeevesSqlExecutor = jeevesSqlExecutor;
        }

        public async Task<IReadOnlyList<InvoiceDto>> GetAllInvoicesAsync(string connectionString, GetInvoicesQuery query)
        {
            return await _jeevesSqlExecutor.QueryAsync<InvoiceDto>(
                connectionString,
                GetAllInvoicesSql,
                new
                {
                    CompanyCode = query.CompanyCode,
                    Search = string.IsNullOrWhiteSpace(query.Search) ? null : $"%{query.Search}%",
                    FromDate = query.FromDate?.Date,
                    ToDate = query.ToDate?.Date
                },
                operationName: "BiInvoicesRepository.GetAllInvoices");
        }

        public async Task<PagedInvoicesResultDto> GetInvoicesPageAsync(string connectionString, GetInvoicesQuery query)
        {
            var normalizedTab = string.Equals(query.ActiveTab, "paid", StringComparison.OrdinalIgnoreCase) ? "paid" : "unpaid";
            if (normalizedTab != "paid")
            {
                return new PagedInvoicesResultDto
                {
                    Invoices = Array.Empty<InvoiceDto>(),
                    UsesHistoricalFactSource = true
                };
            }

            var safePage = query.Page.GetValueOrDefault(1) <= 0 ? 1 : query.Page.GetValueOrDefault(1);
            var safePageSize = query.PageSize.GetValueOrDefault(50) <= 0 ? 50 : query.PageSize.GetValueOrDefault(50);
            var startRow = ((safePage - 1) * safePageSize) + 1;
            var endRow = safePage * safePageSize;

            return await _jeevesSqlExecutor.WithConnectionAsync(
                connectionString,
                async connection =>
                {
                    using var multi = await connection.QueryMultipleAsync(
                        GetInvoicesPageSql,
                        new
                        {
                            CompanyCode = query.CompanyCode,
                            Search = string.IsNullOrWhiteSpace(query.Search) ? null : $"%{query.Search}%",
                            FromDate = query.FromDate?.Date,
                            ToDate = query.ToDate?.Date,
                            StartRow = startRow,
                            EndRow = endRow
                        },
                        commandTimeout: 30);

                    var summary = await multi.ReadFirstOrDefaultAsync<PagedInvoicesSummaryRow>();
                    var countRow = await multi.ReadFirstOrDefaultAsync<PagedInvoicesCountRow>();
                    var rows = (await multi.ReadAsync<InvoiceDto>()).ToList();

                    return new PagedInvoicesResultDto
                    {
                        Invoices = rows,
                        TotalCount = countRow?.TotalCount ?? 0,
                        TotalUnpaidSek = summary?.TotalUnpaidSek ?? 0m,
                        TotalPaidSek = summary?.TotalPaidSek ?? 0m,
                        UnpaidCount = summary?.UnpaidCount ?? 0,
                        PaidCount = summary?.PaidCount ?? 0,
                        OverdueCount = summary?.OverdueCount ?? 0,
                        UsesHistoricalFactSource = true
                    };
                },
                operationName: "BiInvoicesRepository.GetInvoicesPage");
        }

        public async Task<InvoiceDto?> GetInvoiceAsync(string connectionString, GetInvoiceQuery query)
        {
            return await _jeevesSqlExecutor.QueryFirstOrDefaultAsync<InvoiceDto>(
                connectionString,
                GetInvoiceSql,
                new { query.InvoiceNo, query.CompanyCode },
                operationName: "BiInvoicesRepository.GetInvoice");
        }

        public async Task<DateTime?> GetLatestInvoiceDateAsync(string connectionString, int? companyCode)
        {
            return await _jeevesSqlExecutor.QueryFirstOrDefaultAsync<DateTime?>(
                connectionString,
                GetLatestInvoiceDateSql,
                new { CompanyCode = companyCode },
                operationName: "BiInvoicesRepository.GetLatestInvoiceDate");
        }

        public async Task<InvoiceDashboardSummaryDto> GetDashboardSummaryAsync(string connectionString, int? companyCode)
        {
            var totals = await _jeevesSqlExecutor.QueryFirstOrDefaultAsync<InvoiceDashboardSummaryRow>(
                connectionString,
                GetDashboardSummarySql,
                new { CompanyCode = companyCode },
                operationName: "BiInvoicesRepository.GetDashboardSummary");

            return new InvoiceDashboardSummaryDto
            {
                TotalUnpaidSek = totals?.TotalUnpaidSek ?? 0m,
                TotalPaidSek = totals?.TotalPaidSek ?? 0m,
                UnpaidCount = 0,
                UsesHistoricalFactSource = true,
                OverdueInvoices = Array.Empty<InvoiceDto>()
            };
        }

        private sealed class InvoiceDashboardSummaryRow
        {
            public decimal TotalUnpaidSek { get; set; }
            public decimal TotalPaidSek { get; set; }
        }

        private sealed class PagedInvoicesSummaryRow
        {
            public decimal TotalUnpaidSek { get; set; }
            public decimal TotalPaidSek { get; set; }
            public int UnpaidCount { get; set; }
            public int PaidCount { get; set; }
            public int OverdueCount { get; set; }
        }

        private sealed class PagedInvoicesCountRow
        {
            public int TotalCount { get; set; }
        }
    }
}

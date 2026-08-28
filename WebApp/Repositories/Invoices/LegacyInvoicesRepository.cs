using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Repository.Execution;
using WebApp.Models.Invoices;

namespace WebApp.Repositories.Invoices
{
    public class LegacyInvoicesRepository : ILegacyInvoicesRepository
    {
        private const string GetAllInvoicesSql = @"
SELECT
    CAST(fh.FaktNr AS varchar(50))           AS InvoiceNo,
    MAX(fh.FtgNr)                            AS Customer,
    MAX(fh.Saljare)                          AS SalesPerson,
    MAX(fh.FaktDat)                          AS InvoiceDate,
    MAX(COALESCE(fh.FaktFfDat, fh.FaktDat))  AS DueDate,
    MAX(fh.InbDat)                           AS PaidDate,
    MAX(fh.FaktTotMMoms)                     AS AmountInclVat,
    MAX(fh.FaktTotUMoms)                     AS AmountExclVat,
    MAX(fh.MottagetBelopp)                   AS PaidAmount,
    MAX(fh.AttBetalaBelopp)                  AS RemainingAmount,
    MAX(fh.FaktNr_OCR)                       AS Ocr,
    MAX(CAST(fh.ForetagKod AS varchar(50)))  AS CompanyCode
FROM dbo.fh fh
WHERE (@CompanyCode IS NULL OR fh.ForetagKod = @CompanyCode)
  AND (@Search IS NULL OR CAST(fh.FaktNr AS varchar(50)) LIKE @Search OR fh.FtgNr LIKE @Search OR fh.Saljare LIKE @Search OR fh.FaktNr_OCR LIKE @Search)
  AND (@FromDate IS NULL OR fh.FaktDat >= @FromDate)
  AND (@ToDate IS NULL OR fh.FaktDat <= @ToDate)
GROUP BY fh.FaktNr
ORDER BY MAX(fh.FaktDat) DESC;";

        private const string GetInvoicesPageSql = @"
WITH grouped AS (
    SELECT
        CAST(fh.FaktNr AS varchar(50))          AS InvoiceNo,
        MAX(fh.FtgNr)                           AS Customer,
        MAX(fh.Saljare)                         AS SalesPerson,
        MAX(fh.FaktDat)                         AS InvoiceDate,
        MAX(COALESCE(fh.FaktFfDat, fh.FaktDat)) AS DueDate,
        MAX(fh.InbDat)                          AS PaidDate,
        MAX(fh.FaktTotMMoms)                    AS AmountInclVat,
        MAX(fh.FaktTotUMoms)                    AS AmountExclVat,
        MAX(fh.MottagetBelopp)                  AS PaidAmount,
        MAX(fh.AttBetalaBelopp)                 AS RemainingAmount,
        MAX(fh.FaktNr_OCR)                      AS Ocr,
        MAX(CAST(fh.ForetagKod AS varchar(50))) AS CompanyCode
    FROM dbo.fh fh
    WHERE (@CompanyCode IS NULL OR fh.ForetagKod = @CompanyCode)
      AND (@Search IS NULL OR CAST(fh.FaktNr AS varchar(50)) LIKE @Search OR fh.FtgNr LIKE @Search OR fh.Saljare LIKE @Search OR fh.FaktNr_OCR LIKE @Search)
      AND (@FromDate IS NULL OR fh.FaktDat >= @FromDate)
      AND (@ToDate IS NULL OR fh.FaktDat <= @ToDate)
    GROUP BY fh.FaktNr
)
SELECT *
INTO #GroupedInvoices
FROM grouped;

SELECT *
INTO #FilteredInvoices
FROM #GroupedInvoices
WHERE (
    @ActiveTab = 'paid' AND RemainingAmount <= 0 AND AmountInclVat > 0
) OR (
    @ActiveTab <> 'paid' AND RemainingAmount > 0
);

SELECT
    CAST(SUM(CASE WHEN RemainingAmount > 0 THEN AmountInclVat ELSE 0 END) AS decimal(18,2)) AS TotalUnpaidSek,
    CAST(SUM(CASE WHEN RemainingAmount <= 0 AND AmountInclVat > 0 THEN AmountInclVat ELSE 0 END) AS decimal(18,2)) AS TotalPaidSek,
    SUM(CASE WHEN RemainingAmount > 0 THEN 1 ELSE 0 END) AS UnpaidCount,
    SUM(CASE WHEN RemainingAmount <= 0 AND AmountInclVat > 0 THEN 1 ELSE 0 END) AS PaidCount,
    SUM(CASE WHEN RemainingAmount > 0 AND DueDate < CAST(GETDATE() AS date) THEN 1 ELSE 0 END) AS OverdueCount
FROM #GroupedInvoices;

SELECT COUNT(1) AS TotalCount
FROM #FilteredInvoices;

WITH numbered AS (
    SELECT
        *,
        ROW_NUMBER() OVER (
            ORDER BY
                CASE WHEN @ActiveTab = 'paid' THEN PaidDate END DESC,
                CASE WHEN @ActiveTab <> 'paid' THEN DueDate END ASC,
                InvoiceNo DESC
        ) AS RowNum
    FROM #FilteredInvoices
)
SELECT *
FROM numbered
WHERE RowNum BETWEEN @StartRow AND @EndRow
ORDER BY RowNum;

DROP TABLE #FilteredInvoices;
DROP TABLE #GroupedInvoices;";

        private const string GetInvoiceSql = @"
SELECT TOP (1)
    CAST(fh.FaktNr AS varchar(50))           AS InvoiceNo,
    MAX(fh.FtgNr)                            AS Customer,
    MAX(fh.Saljare)                          AS SalesPerson,
    MAX(fh.FaktDat)                          AS InvoiceDate,
    MAX(COALESCE(fh.FaktFfDat, fh.FaktDat))  AS DueDate,
    MAX(fh.InbDat)                           AS PaidDate,
    MAX(fh.FaktTotMMoms)                     AS AmountInclVat,
    MAX(fh.FaktTotUMoms)                     AS AmountExclVat,
    MAX(fh.MottagetBelopp)                   AS PaidAmount,
    MAX(fh.AttBetalaBelopp)                  AS RemainingAmount,
    MAX(fh.FaktNr_OCR)                       AS Ocr,
    MAX(CAST(fh.ForetagKod AS varchar(50)))  AS CompanyCode
FROM dbo.fh fh
WHERE CAST(fh.FaktNr AS varchar(50)) = @InvoiceNo
  AND (@CompanyCode IS NULL OR fh.ForetagKod = @CompanyCode)
GROUP BY fh.FaktNr
ORDER BY MAX(fh.FaktDat) DESC;";

        private const string GetLatestInvoiceDateSql = @"
SELECT MAX(fh.FaktDat)
FROM dbo.fh fh
WHERE (@CompanyCode IS NULL OR fh.ForetagKod = @CompanyCode);";

        private const string GetDashboardSummarySql = @"
SELECT
    CAST(SUM(CASE WHEN fh.AttBetalaBelopp > 0 THEN fh.FaktTotMMoms ELSE 0 END) AS decimal(18,2)) AS TotalUnpaidSek,
    CAST(SUM(CASE WHEN fh.AttBetalaBelopp <= 0 AND fh.FaktTotMMoms > 0 THEN fh.FaktTotMMoms ELSE 0 END) AS decimal(18,2)) AS TotalPaidSek,
    SUM(CASE WHEN fh.AttBetalaBelopp > 0 THEN 1 ELSE 0 END) AS UnpaidCount
FROM dbo.fh fh
WHERE (@CompanyCode IS NULL OR fh.ForetagKod = @CompanyCode);

SELECT TOP (3)
    CAST(fh.FaktNr AS varchar(50))           AS InvoiceNo,
    MAX(fh.FtgNr)                            AS Customer,
    MAX(fh.Saljare)                          AS SalesPerson,
    MAX(fh.FaktDat)                          AS InvoiceDate,
    MAX(COALESCE(fh.FaktFfDat, fh.FaktDat))  AS DueDate,
    MAX(fh.InbDat)                           AS PaidDate,
    MAX(fh.FaktTotMMoms)                     AS AmountInclVat,
    MAX(fh.FaktTotUMoms)                     AS AmountExclVat,
    MAX(fh.MottagetBelopp)                   AS PaidAmount,
    MAX(fh.AttBetalaBelopp)                  AS RemainingAmount,
    MAX(fh.FaktNr_OCR)                       AS Ocr,
    MAX(CAST(fh.ForetagKod AS varchar(50)))  AS CompanyCode
FROM dbo.fh fh
WHERE (@CompanyCode IS NULL OR fh.ForetagKod = @CompanyCode)
  AND fh.AttBetalaBelopp > 0
  AND COALESCE(fh.FaktFfDat, fh.FaktDat) < CAST(GETDATE() AS date)
GROUP BY fh.FaktNr
ORDER BY MAX(COALESCE(fh.FaktFfDat, fh.FaktDat)) ASC, MAX(fh.FaktTotMMoms) DESC;";

        private readonly IJeevesSqlExecutor _jeevesSqlExecutor;

        public LegacyInvoicesRepository(IJeevesSqlExecutor jeevesSqlExecutor)
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
                operationName: "LegacyInvoicesRepository.GetAllInvoices");
        }

        public async Task<PagedInvoicesResultDto> GetInvoicesPageAsync(string connectionString, GetInvoicesQuery query)
        {
            var normalizedTab = string.Equals(query.ActiveTab, "paid", StringComparison.OrdinalIgnoreCase) ? "paid" : "unpaid";
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
                            ActiveTab = normalizedTab,
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
                        OverdueCount = summary?.OverdueCount ?? 0
                    };
                },
                operationName: "LegacyInvoicesRepository.GetInvoicesPage");
        }

        public async Task<InvoiceDto?> GetInvoiceAsync(string connectionString, GetInvoiceQuery query)
        {
            return await _jeevesSqlExecutor.QueryFirstOrDefaultAsync<InvoiceDto>(
                connectionString,
                GetInvoiceSql,
                new { query.InvoiceNo, query.CompanyCode },
                operationName: "LegacyInvoicesRepository.GetInvoice");
        }

        public async Task<DateTime?> GetLatestInvoiceDateAsync(string connectionString, int? companyCode)
        {
            return await _jeevesSqlExecutor.QueryFirstOrDefaultAsync<DateTime?>(
                connectionString,
                GetLatestInvoiceDateSql,
                new { CompanyCode = companyCode },
                operationName: "LegacyInvoicesRepository.GetLatestInvoiceDate");
        }

        public async Task<InvoiceDashboardSummaryDto> GetDashboardSummaryAsync(string connectionString, int? companyCode)
        {
            return await _jeevesSqlExecutor.WithConnectionAsync(
                connectionString,
                async connection =>
                {
                    using var multi = await connection.QueryMultipleAsync(GetDashboardSummarySql, new { CompanyCode = companyCode }, commandTimeout: 15);
                    var totals = await multi.ReadFirstOrDefaultAsync<InvoiceDashboardSummaryRow>();
                    var overdue = (await multi.ReadAsync<InvoiceDto>()).ToList();

                    return new InvoiceDashboardSummaryDto
                    {
                        TotalUnpaidSek = totals?.TotalUnpaidSek ?? 0m,
                        TotalPaidSek = totals?.TotalPaidSek ?? 0m,
                        UnpaidCount = totals?.UnpaidCount ?? 0,
                        OverdueInvoices = overdue
                    };
                },
                operationName: "LegacyInvoicesRepository.GetDashboardSummary");
        }

        private sealed class InvoiceDashboardSummaryRow
        {
            public decimal TotalUnpaidSek { get; set; }
            public decimal TotalPaidSek { get; set; }
            public int UnpaidCount { get; set; }
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

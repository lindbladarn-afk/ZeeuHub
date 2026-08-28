using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Repository.Execution;
using WebApp.Models.Orders;

namespace WebApp.Repositories.Orders
{
    // Reads orders only from the operational Jeeves tables.
    // This repository assumes the caller has already chosen the legacy source.
    public sealed class LegacyOrdersRepository : ILegacyOrdersRepository
    {
        private readonly IJeevesSqlExecutor _jeevesSqlExecutor;

        public LegacyOrdersRepository(IJeevesSqlExecutor jeevesSqlExecutor)
        {
            _jeevesSqlExecutor = jeevesSqlExecutor;
        }

        public async Task<PagedOrdersPageResultDto> GetOrdersPageAsync(string connectionString, GetOrdersQuery query)
        {
            var orderBy = (query.Sort ?? string.Empty).ToLowerInvariant() switch
            {
                "customer" => "CustomerName",
                "amount" => "AmountInclVat",
                _ => "OrderDate"
            };
            var direction = query.Desc ? "DESC" : "ASC";
            var normalizedPaymentFilter = string.IsNullOrWhiteSpace(query.PaymentFilter)
                ? "all"
                : query.PaymentFilter.Trim().ToLowerInvariant();
            var safePage = query.Page <= 0 ? 1 : query.Page;
            var safePageSize = query.PageSize <= 0 ? 50 : query.PageSize;
            var startRow = ((safePage - 1) * safePageSize) + 1;
            var endRow = safePage * safePageSize;
            const string paidOrderPredicate = "(oh.OrderAvslutad IN ('1','J','Y','True','true') OR LTRIM(RTRIM(CAST(oh.OrdStat AS varchar(20)))) = '70')";

            var sql = $@"
WITH filtered AS (
    SELECT
        COALESCE(fr.FtgNamn, oh.OrdBeskr, oh.FtgNr, '') AS [CustomerName],
        oh.OrdDatum              AS [OrderDate],
        oh.OrderNr               AS [OrderNo],
        COALESCE(NULLIF(orderTotals.LineAmountInclVat, 0), oh.OrdSumInklMoms, 0) AS [AmountInclVat]
    FROM dbo.oh oh
    LEFT JOIN dbo.fr fr ON fr.ForetagKod = oh.ForetagKod AND fr.FtgNr = oh.FtgNr
    OUTER APPLY (
        SELECT
            SUM(orp.vb_RadVardeInklMoms) AS LineAmountInclVat
        FROM dbo.orp orp
        WHERE orp.OrderNr = oh.OrderNr
          AND orp.ForetagKod = oh.ForetagKod
    ) orderTotals
    WHERE (@CompanyCode IS NULL OR oh.ForetagKod = @CompanyCode)
      AND (@Search IS NULL OR oh.OrderNrAlfa LIKE @Search OR CAST(oh.OrderNr AS varchar(50)) LIKE @Search OR oh.FtgNr LIKE @Search OR oh.OrdBeskr LIKE @Search OR fr.FtgNamn LIKE @Search)
      AND (@FromDate IS NULL OR oh.OrdDatum >= @FromDate)
      AND (@ToDate IS NULL OR oh.OrdDatum <= @ToDate)
      AND (
            @PaymentFilter = 'all'
            OR (@PaymentFilter = 'paid' AND {paidOrderPredicate})
            OR (@PaymentFilter = 'unpaid' AND NOT {paidOrderPredicate})
      )
)
-- Materialize the filtered key set once so count and page rows are derived from the same snapshot.
SELECT *
INTO #FilteredOrderKeys
FROM filtered;

SELECT COUNT(1) AS [TotalCount]
FROM #FilteredOrderKeys;

WITH numbered AS (
    SELECT
        *,
        ROW_NUMBER() OVER (ORDER BY {orderBy} {direction}, OrderNo DESC) AS RowNum
    FROM #FilteredOrderKeys
)
SELECT
    oh.OrderNr               AS [OrderNo],
    oh.OrderNrAlfa           AS [OrderNoAlfa],
    oh.FtgNr                 AS [CustomerNo],
    COALESCE(fr.FtgNamn, oh.OrdBeskr, oh.FtgNr, '') AS [CustomerName],
    oh.OrdBeskr              AS [Description],
    oh.OrdDatum              AS [OrderDate],
    oh.OrdBerLevDat          AS [PlannedDelivery],
    oh.OrdLovLevDat          AS [PromisedDate],
    oh.OrdVerklLevDat        AS [ActualDelivery],
    COALESCE(NULLIF(orderTotals.LineAmountExclVat, 0), oh.OrdSumExklMoms, 0) AS [AmountExclVat],
    COALESCE(NULLIF(orderTotals.LineAmountInclVat, 0), oh.OrdSumInklMoms, 0) AS [AmountInclVat],
    oh.ValKod                AS [Currency],
    oh.OrdStat               AS [StatusCode],
    oh.OrdTyp                AS [OrderType],
    oh.Saljare               AS [SalesPerson],
    CAST(oh.ForetagKod AS varchar(50)) AS [CompanyCode],
    CASE WHEN {paidOrderPredicate} THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS [IsClosed]
FROM numbered
INNER JOIN dbo.oh oh
    ON oh.OrderNr = numbered.OrderNo
   AND (@CompanyCode IS NULL OR oh.ForetagKod = @CompanyCode)
LEFT JOIN dbo.fr fr
    ON fr.ForetagKod = oh.ForetagKod
   AND fr.FtgNr = oh.FtgNr
OUTER APPLY (
    SELECT
        SUM(orp.vb_RadVardeExklMoms) AS LineAmountExclVat,
        SUM(orp.vb_RadVardeInklMoms) AS LineAmountInclVat
    FROM dbo.orp orp
    WHERE orp.OrderNr = oh.OrderNr
      AND orp.ForetagKod = oh.ForetagKod
) orderTotals
WHERE RowNum BETWEEN @StartRow AND @EndRow
ORDER BY RowNum;

DROP TABLE #FilteredOrderKeys;";

            return await _jeevesSqlExecutor.WithConnectionAsync(
                connectionString,
                async connection =>
                {
                    using var multi = await connection.QueryMultipleAsync(
                        sql,
                        new
                        {
                            CompanyCode = query.CompanyCode,
                            Search = string.IsNullOrWhiteSpace(query.Search) ? null : $"%{query.Search}%",
                            FromDate = query.FromDate?.Date,
                            ToDate = query.ToDate?.Date,
                            PaymentFilter = normalizedPaymentFilter,
                            StartRow = startRow,
                            EndRow = endRow
                        },
                        commandTimeout: 30);

                    var countRow = await multi.ReadFirstOrDefaultAsync<PagedOrdersCountRow>();
                    var rows = (await multi.ReadAsync<OrderHeaderDto>()).ToList();

                    return new PagedOrdersPageResultDto
                    {
                        Orders = rows,
                        TotalCount = countRow?.TotalCount ?? 0
                    };
                },
                operationName: "LegacyOrdersRepository.GetOrdersPage");
        }

        public async Task<OrdersSummaryDto> GetOrdersSummaryAsync(string connectionString, GetOrdersQuery query)
        {
            var normalizedPaymentFilter = string.IsNullOrWhiteSpace(query.PaymentFilter)
                ? "all"
                : query.PaymentFilter.Trim().ToLowerInvariant();
            const string paidOrderPredicate = "(oh.OrderAvslutad IN ('1','J','Y','True','true') OR LTRIM(RTRIM(CAST(oh.OrdStat AS varchar(20)))) = '70')";

            var sql = $@"
WITH grouped AS (
    SELECT
        COALESCE(NULLIF(SUM(orp.vb_RadVardeInklMoms), 0), oh.OrdSumInklMoms, 0) AS AmountInclVat,
        CASE WHEN {paidOrderPredicate} THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsClosed
    FROM dbo.oh oh
    LEFT JOIN dbo.fr fr ON fr.ForetagKod = oh.ForetagKod AND fr.FtgNr = oh.FtgNr
    LEFT JOIN dbo.orp orp ON orp.OrderNr = oh.OrderNr AND orp.ForetagKod = oh.ForetagKod
    WHERE (@CompanyCode IS NULL OR oh.ForetagKod = @CompanyCode)
      AND (@Search IS NULL OR oh.OrderNrAlfa LIKE @Search OR CAST(oh.OrderNr AS varchar(50)) LIKE @Search OR oh.FtgNr LIKE @Search OR oh.OrdBeskr LIKE @Search OR fr.FtgNamn LIKE @Search)
      AND (@FromDate IS NULL OR oh.OrdDatum >= @FromDate)
      AND (@ToDate IS NULL OR oh.OrdDatum <= @ToDate)
      AND (
            @PaymentFilter = 'all'
            OR (@PaymentFilter = 'paid' AND {paidOrderPredicate})
            OR (@PaymentFilter = 'unpaid' AND NOT {paidOrderPredicate})
      )
    GROUP BY oh.OrderNr, oh.OrdSumInklMoms, oh.OrderAvslutad, oh.OrdStat
)
SELECT
    CAST(SUM(CASE WHEN IsClosed = 1 THEN AmountInclVat ELSE 0 END) AS decimal(18,2)) AS [PaidAmountTotal],
    CAST(SUM(CASE WHEN IsClosed = 0 THEN AmountInclVat ELSE 0 END) AS decimal(18,2)) AS [UnpaidAmountTotal],
    CAST(SUM(AmountInclVat) AS decimal(18,2)) AS [GrandAmountTotal]
FROM grouped;";

            var summary = await _jeevesSqlExecutor.QueryFirstOrDefaultAsync<OrdersSummaryDto>(
                connectionString,
                sql,
                new
                {
                    CompanyCode = query.CompanyCode,
                    Search = string.IsNullOrWhiteSpace(query.Search) ? null : $"%{query.Search}%",
                    FromDate = query.FromDate?.Date,
                    ToDate = query.ToDate?.Date,
                    PaymentFilter = normalizedPaymentFilter
                },
                operationName: "LegacyOrdersRepository.GetOrdersSummary");

            return summary ?? new OrdersSummaryDto();
        }

        public async Task<DateTime?> GetLatestOrderDateAsync(string connectionString, int? companyCode)
        {
            const string sql = @"
SELECT MAX(oh.OrdDatum)
FROM dbo.oh oh
WHERE (@CompanyCode IS NULL OR oh.ForetagKod = @CompanyCode);";

            return await _jeevesSqlExecutor.QueryFirstOrDefaultAsync<DateTime?>(
                connectionString,
                sql,
                new { CompanyCode = companyCode },
                operationName: "LegacyOrdersRepository.GetLatestOrderDate");
        }

        public async Task<OrderDeliveryInsightSummaryDto> GetOverdueDeliverySummaryAsync(string connectionString, GetOrderDeliveryInsightQuery query)
        {
            return await GetDeliveryInsightSummaryAsync(
                connectionString,
                query,
                comparisonOperator: "<",
                operationName: "LegacyOrdersRepository.GetOverdueDeliverySummary");
        }

        public async Task<OrderDeliveryInsightSummaryDto> GetFutureDeliverySummaryAsync(string connectionString, GetDeliveryForecastQuery query)
        {
            return await GetDeliveryInsightSummaryAsync(
                connectionString,
                new GetOrderDeliveryInsightQuery
                {
                    CompanyCode = query.CompanyCode,
                    CustomerNo = query.CustomerNo
                },
                comparisonOperator: ">",
                operationName: "LegacyOrdersRepository.GetFutureDeliverySummary");
        }

        public async Task<IReadOnlyList<OrderDeliveryTimelineBucketDto>> GetFutureDeliveryTimelineAsync(string connectionString, GetDeliveryForecastQuery query)
        {
            var safeMonthsAhead = query.MonthsAhead <= 0 ? 6 : query.MonthsAhead;
            const string paidOrderPredicate = "(oh.OrderAvslutad IN ('1','J','Y','True','true') OR LTRIM(RTRIM(CAST(oh.OrdStat AS varchar(20)))) = '70')";
            var legacySql = $@"
WITH grouped AS (
    SELECT
        DATEFROMPARTS(YEAR(COALESCE(oh.OrdLovLevDat, oh.OrdBerLevDat)), MONTH(COALESCE(oh.OrdLovLevDat, oh.OrdBerLevDat)), 1) AS [PeriodStart],
        oh.OrderNr AS [OrderNo],
        COALESCE(NULLIF(SUM(orp.vb_RadVardeInklMoms), 0), oh.OrdSumInklMoms, 0) AS [AmountInclVat]
    FROM dbo.oh oh
    LEFT JOIN dbo.orp orp
        ON orp.OrderNr = oh.OrderNr
       AND orp.ForetagKod = oh.ForetagKod
    WHERE (@CompanyCode IS NULL OR oh.ForetagKod = @CompanyCode)
      AND (@CustomerNo IS NULL OR oh.FtgNr = @CustomerNo)
      AND NOT {paidOrderPredicate}
      AND COALESCE(oh.OrdLovLevDat, oh.OrdBerLevDat) IS NOT NULL
      AND CAST(COALESCE(oh.OrdLovLevDat, oh.OrdBerLevDat) AS date) > CAST(GETDATE() AS date)
      AND DATEFROMPARTS(YEAR(COALESCE(oh.OrdLovLevDat, oh.OrdBerLevDat)), MONTH(COALESCE(oh.OrdLovLevDat, oh.OrdBerLevDat)), 1)
            < DATEADD(MONTH, @MonthsAhead, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1))
    GROUP BY
        oh.OrderNr,
        oh.OrdLovLevDat,
        oh.OrdBerLevDat,
        oh.OrdSumInklMoms
)
SELECT
    [PeriodStart],
    COUNT(1) AS [OrderCount],
    CAST(ISNULL(SUM(AmountInclVat), 0) AS decimal(18,2)) AS [AmountTotal]
FROM grouped
GROUP BY [PeriodStart]
ORDER BY [PeriodStart];";

            return await _jeevesSqlExecutor.QueryAsync<OrderDeliveryTimelineBucketDto>(
                connectionString,
                legacySql,
                new
                {
                    CompanyCode = query.CompanyCode,
                    CustomerNo = string.IsNullOrWhiteSpace(query.CustomerNo) ? null : query.CustomerNo.Trim(),
                    MonthsAhead = safeMonthsAhead
                },
                operationName: "LegacyOrdersRepository.GetFutureDeliveryTimeline");
        }

        public async Task<OrderWithLinesDto?> GetOrderWithLinesAsync(string connectionString, GetOrderDetailsQuery query)
        {
            const string paidOrderPredicate = "(oh.OrderAvslutad IN ('1','J','Y','True','true') OR LTRIM(RTRIM(CAST(oh.OrdStat AS varchar(20)))) = '70')";

            var headerSql = $@"
SELECT
    oh.OrderNr               AS [OrderNo],
    oh.OrderNrAlfa           AS [OrderNoAlfa],
    oh.FtgNr                 AS [CustomerNo],
    COALESCE(fr.FtgNamn, '') AS [CustomerName],
    oh.OrdBeskr              AS [Description],
    oh.OrdDatum              AS [OrderDate],
    oh.OrdBerLevDat          AS [PlannedDelivery],
    oh.OrdLovLevDat          AS [PromisedDate],
    oh.OrdVerklLevDat        AS [ActualDelivery],
    COALESCE(NULLIF(orderTotals.LineAmountExclVat, 0), oh.OrdSumExklMoms, 0) AS [AmountExclVat],
    COALESCE(NULLIF(orderTotals.LineAmountInclVat, 0), oh.OrdSumInklMoms, 0) AS [AmountInclVat],
    oh.ValKod                AS [Currency],
    oh.OrdStat               AS [StatusCode],
    oh.OrdTyp                AS [OrderType],
    oh.Saljare               AS [SalesPerson],
    oh.ForetagKod            AS [CompanyCode],
    CASE WHEN {paidOrderPredicate} THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS [IsClosed]
FROM dbo.oh oh
LEFT JOIN dbo.fr fr ON fr.ForetagKod = oh.ForetagKod AND fr.FtgNr = oh.FtgNr
OUTER APPLY (
    SELECT
        SUM(orp.vb_RadVardeExklMoms) AS LineAmountExclVat,
        SUM(orp.vb_RadVardeInklMoms) AS LineAmountInclVat
    FROM dbo.orp orp
    WHERE orp.OrderNr = oh.OrderNr
      AND orp.ForetagKod = oh.ForetagKod
) orderTotals
WHERE oh.OrderNr = @OrderNo
  AND (@CompanyCode IS NULL OR oh.ForetagKod = @CompanyCode)";

            const string linesSql = @"
SELECT
    orp.OrderNr              AS [OrderNo],
    orp.OrdRadNr             AS [LineNo],
    orp.ArtNr                AS [ArticleNo],
    COALESCE(orp.OrdArtBeskr, orp.ArtBeskr, orp.ArtNr) AS [ArticleDescription],
    orp.OrdAntal             AS [OrderedQty],
    orp.OrdLevAntal          AS [DeliveredQty],
    orp.OrdRestAnt           AS [RestQty],
    orp.EnhetsKod            AS [Unit],
    orp.NettoPris            AS [NetPrice],
    orp.vb_RadVardeExklMoms  AS [LineAmountExclVat],
    orp.vb_RadVardeInklMoms  AS [LineAmountInclVat],
    orp.rabatt               AS [DiscountPercent],
    orp.rabattval            AS [DiscountValue],
    orp.ValKod               AS [Currency]
FROM dbo.orp orp
WHERE orp.OrderNr = @OrderNo
  AND (@CompanyCode IS NULL OR orp.ForetagKod = @CompanyCode)
ORDER BY orp.OrdRadNr";

            var header = await _jeevesSqlExecutor.QueryFirstOrDefaultAsync<OrderHeaderDto>(
                connectionString,
                headerSql,
                new { OrderNo = query.OrderNo, CompanyCode = query.CompanyCode },
                operationName: "LegacyOrdersRepository.GetOrderHeader");
            if (header == null)
            {
                return null;
            }

            var lines = await _jeevesSqlExecutor.QueryAsync<OrderLineDto>(
                connectionString,
                linesSql,
                new { OrderNo = query.OrderNo, CompanyCode = query.CompanyCode },
                operationName: "LegacyOrdersRepository.GetOrderLines");

            return new OrderWithLinesDto
            {
                Header = header,
                Lines = lines
            };
        }

        public async Task<IReadOnlyList<OrderCustomerOption>> GetFutureDeliveryCustomerOptionsAsync(string connectionString, GetDeliveryForecastQuery query)
        {
            var safeMonthsAhead = query.MonthsAhead <= 0 ? 6 : query.MonthsAhead;
            const string legacySql = @"
SELECT
    oh.FtgNr AS [CustomerNo],
    MAX(COALESCE(fr.FtgNamn, oh.OrdBeskr, oh.FtgNr)) AS [CustomerName]
FROM dbo.oh oh
LEFT JOIN dbo.fr fr
    ON fr.ForetagKod = oh.ForetagKod
   AND fr.FtgNr = oh.FtgNr
WHERE (@CompanyCode IS NULL OR oh.ForetagKod = @CompanyCode)
  AND COALESCE(oh.OrdLovLevDat, oh.OrdBerLevDat) IS NOT NULL
  AND CAST(COALESCE(oh.OrdLovLevDat, oh.OrdBerLevDat) AS date) > CAST(GETDATE() AS date)
  AND DATEFROMPARTS(YEAR(COALESCE(oh.OrdLovLevDat, oh.OrdBerLevDat)), MONTH(COALESCE(oh.OrdLovLevDat, oh.OrdBerLevDat)), 1)
      < DATEADD(MONTH, @MonthsAhead, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1))
  AND NOT (oh.OrderAvslutad IN ('1','J','Y','True','true') OR LTRIM(RTRIM(CAST(oh.OrdStat AS varchar(20)))) = '70')
GROUP BY oh.FtgNr
ORDER BY [CustomerName], [CustomerNo];";

            return await _jeevesSqlExecutor.QueryAsync<OrderCustomerOption>(
                connectionString,
                legacySql,
                new { CompanyCode = query.CompanyCode, MonthsAhead = safeMonthsAhead },
                operationName: "LegacyOrdersRepository.GetFutureDeliveryCustomerOptions");
        }

        public async Task<PagedOrdersPageResultDto> GetUpcomingOrdersPageAsync(string connectionString, GetDeliveryForecastQuery query)
        {
            var safeMonthsAhead = query.MonthsAhead <= 0 ? 6 : query.MonthsAhead;
            var safePage = query.Page <= 0 ? 1 : query.Page;
            var safePageSize = query.PageSize <= 0 ? 25 : query.PageSize;
            var startRow = ((safePage - 1) * safePageSize) + 1;
            var endRow = safePage * safePageSize;

            const string legacySql = @"
WITH grouped AS (
    SELECT
        oh.OrderNr AS [OrderNo],
        oh.OrderNrAlfa AS [OrderNoAlfa],
        oh.FtgNr AS [CustomerNo],
        COALESCE(fr.FtgNamn, oh.OrdBeskr, oh.FtgNr, '') AS [CustomerName],
        oh.OrdBeskr AS [Description],
        oh.OrdDatum AS [OrderDate],
        oh.OrdBerLevDat AS [PlannedDelivery],
        oh.OrdLovLevDat AS [PromisedDate],
        oh.OrdVerklLevDat AS [ActualDelivery],
        COALESCE(NULLIF(SUM(orp.vb_RadVardeExklMoms), 0), oh.OrdSumExklMoms, 0) AS [AmountExclVat],
        COALESCE(NULLIF(SUM(orp.vb_RadVardeInklMoms), 0), oh.OrdSumInklMoms, 0) AS [AmountInclVat],
        oh.ValKod AS [Currency],
        CAST(oh.OrdStat AS varchar(50)) AS [StatusCode],
        CAST(oh.OrdTyp AS varchar(50)) AS [OrderType],
        oh.Saljare AS [SalesPerson],
        CAST(oh.ForetagKod AS varchar(50)) AS [CompanyCode],
        CAST(0 AS bit) AS [IsClosed]
    FROM dbo.oh oh
    LEFT JOIN dbo.fr fr
        ON fr.ForetagKod = oh.ForetagKod
       AND fr.FtgNr = oh.FtgNr
    LEFT JOIN dbo.orp orp
        ON orp.OrderNr = oh.OrderNr
       AND orp.ForetagKod = oh.ForetagKod
    WHERE (@CompanyCode IS NULL OR oh.ForetagKod = @CompanyCode)
      AND (@CustomerNo IS NULL OR oh.FtgNr = @CustomerNo)
      AND NOT (oh.OrderAvslutad IN ('1','J','Y','True','true') OR LTRIM(RTRIM(CAST(oh.OrdStat AS varchar(20)))) = '70')
      AND COALESCE(oh.OrdLovLevDat, oh.OrdBerLevDat) IS NOT NULL
      AND CAST(COALESCE(oh.OrdLovLevDat, oh.OrdBerLevDat) AS date) > CAST(GETDATE() AS date)
      AND DATEFROMPARTS(YEAR(COALESCE(oh.OrdLovLevDat, oh.OrdBerLevDat)), MONTH(COALESCE(oh.OrdLovLevDat, oh.OrdBerLevDat)), 1)
          < DATEADD(MONTH, @MonthsAhead, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1))
    GROUP BY oh.OrderNr, oh.OrderNrAlfa, oh.FtgNr, fr.FtgNamn, oh.OrdBeskr, oh.OrdDatum, oh.OrdBerLevDat, oh.OrdLovLevDat, oh.OrdVerklLevDat, oh.OrdSumExklMoms, oh.OrdSumInklMoms, oh.ValKod, oh.OrdStat, oh.OrdTyp, oh.Saljare, oh.ForetagKod
)
SELECT *
INTO #UpcomingOrdersLegacy
FROM grouped;

SELECT COUNT(1) AS [TotalCount]
FROM #UpcomingOrdersLegacy;

WITH numbered AS (
    SELECT *,
           ROW_NUMBER() OVER (ORDER BY COALESCE([PromisedDate], [PlannedDelivery], [OrderDate]) ASC, [OrderNo] DESC) AS [RowNum]
    FROM #UpcomingOrdersLegacy
)
SELECT
    [OrderNo],
    [OrderNoAlfa],
    [CustomerNo],
    [CustomerName],
    [Description],
    [OrderDate],
    [PlannedDelivery],
    [PromisedDate],
    [ActualDelivery],
    [AmountExclVat],
    [AmountInclVat],
    [Currency],
    [StatusCode],
    [OrderType],
    [SalesPerson],
    [CompanyCode],
    [IsClosed]
FROM numbered
WHERE [RowNum] BETWEEN @StartRow AND @EndRow
ORDER BY [RowNum];

DROP TABLE #UpcomingOrdersLegacy;";

            return await _jeevesSqlExecutor.WithConnectionAsync(
                connectionString,
                async connection =>
                {
                    using var multi = await connection.QueryMultipleAsync(
                        legacySql,
                        new
                        {
                            CompanyCode = query.CompanyCode,
                            CustomerNo = string.IsNullOrWhiteSpace(query.CustomerNo) ? null : query.CustomerNo.Trim(),
                            MonthsAhead = safeMonthsAhead,
                            StartRow = startRow,
                            EndRow = endRow
                        },
                        commandTimeout: 30);

                    var countRow = await multi.ReadFirstOrDefaultAsync<PagedOrdersCountRow>();
                    var rows = (await multi.ReadAsync<OrderHeaderDto>()).ToList();

                    return new PagedOrdersPageResultDto
                    {
                        Orders = rows,
                        TotalCount = countRow?.TotalCount ?? 0
                    };
                },
                operationName: "LegacyOrdersRepository.GetUpcomingOrdersPage");
        }

        // Shared aggregate used by Action Center so order insights reuse the same order semantics as the list view.
        private async Task<OrderDeliveryInsightSummaryDto> GetDeliveryInsightSummaryAsync(
            string connectionString,
            GetOrderDeliveryInsightQuery query,
            string comparisonOperator,
            string operationName)
        {
            const string paidOrderPredicate = "(oh.OrderAvslutad IN ('1','J','Y','True','true') OR LTRIM(RTRIM(CAST(oh.OrdStat AS varchar(20)))) = '70')";
            var sql = $@"
WITH grouped AS (
    SELECT
        oh.OrderNr AS [OrderNo],
        COALESCE(oh.OrdLovLevDat, oh.OrdBerLevDat) AS [DeliveryDate],
        COALESCE(NULLIF(SUM(orp.vb_RadVardeInklMoms), 0), oh.OrdSumInklMoms, 0) AS [AmountInclVat]
    FROM dbo.oh oh
    LEFT JOIN dbo.orp orp
        ON orp.OrderNr = oh.OrderNr
       AND orp.ForetagKod = oh.ForetagKod
    WHERE (@CompanyCode IS NULL OR oh.ForetagKod = @CompanyCode)
      AND (@CustomerNo IS NULL OR oh.FtgNr = @CustomerNo)
      AND NOT {paidOrderPredicate}
      AND COALESCE(oh.OrdLovLevDat, oh.OrdBerLevDat) IS NOT NULL
      AND CAST(COALESCE(oh.OrdLovLevDat, oh.OrdBerLevDat) AS date) {comparisonOperator} CAST(GETDATE() AS date)
    GROUP BY oh.OrderNr, oh.OrdLovLevDat, oh.OrdBerLevDat, oh.OrdSumInklMoms
)
SELECT
    COUNT(1) AS [OrderCount],
    CAST(ISNULL(SUM(AmountInclVat), 0) AS decimal(18,2)) AS [AmountTotal],
    MIN(DeliveryDate) AS [EarliestDate],
    MAX(DeliveryDate) AS [LatestDate]
FROM grouped;";

            return await _jeevesSqlExecutor.QueryFirstOrDefaultAsync<OrderDeliveryInsightSummaryDto>(
                       connectionString,
                       sql,
                       new
                       {
                           CompanyCode = query.CompanyCode,
                           CustomerNo = string.IsNullOrWhiteSpace(query.CustomerNo) ? null : query.CustomerNo.Trim()
                       },
                       operationName: operationName)
                   ?? new OrderDeliveryInsightSummaryDto();
        }

        private sealed class PagedOrdersCountRow
        {
            public int TotalCount { get; set; }
        }
    }
}

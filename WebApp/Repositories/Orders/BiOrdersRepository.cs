using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Repository.Execution;
using WebApp.Models.Orders;

namespace WebApp.Repositories.Orders
{
    // Reads orders from the BI fact view. This source favors historical and analytical consistency over live ERP state.
    public sealed class BiOrdersRepository : IBiOrdersRepository
    {
        private readonly IJeevesSqlExecutor _jeevesSqlExecutor;

        public BiOrdersRepository(IJeevesSqlExecutor jeevesSqlExecutor)
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

            var sql = $@"
WITH base AS (
    SELECT
        [Company],
        [Order No],
        [Order Date],
        [Customer No],
        [Payer Name],
        [Item No],
        [Item Description],
        [Requested Delivery Date],
        [Estimated Delivery Date],
        [Dispatch Date],
        [Order Row Sum],
        [Order Row Sum BCU],
        [Currency Code],
        [Order Status],
        [Order Status Description],
        [Order Type],
        [Order Type Description],
        [Division],
        [Sales Code],
        [Order Qty],
        [Delivered Qty]
    FROM [dbo].[q_zu_bi_fsg_ord]
    WHERE [Order No] IS NOT NULL
      AND (@CompanyCode IS NULL OR [Company] = @CompanyCode)
      AND (@Search IS NULL
           OR CAST([Order No] AS varchar(50)) LIKE @Search
           OR CAST([Customer No] AS nvarchar(100)) LIKE @Search
           OR CAST([Payer Name] AS nvarchar(255)) LIKE @Search
           OR CAST([Item No] AS nvarchar(100)) LIKE @Search
           OR CAST([Item Description] AS nvarchar(255)) LIKE @Search)
      AND (@FromDate IS NULL OR CAST([Order Date] AS date) >= @FromDate)
      AND (@ToDate IS NULL OR CAST([Order Date] AS date) <= @ToDate)
),
grouped AS (
    SELECT
        CAST([Order No] AS bigint) AS [OrderNo],
        MAX(CAST([Order No] AS varchar(50))) AS [OrderNoAlfa],
        MAX(CAST([Customer No] AS varchar(100))) AS [CustomerNo],
        MAX(COALESCE(NULLIF(CAST([Payer Name] AS nvarchar(255)), ''), CAST([Customer No] AS nvarchar(255)))) AS [CustomerName],
        MAX(COALESCE(NULLIF(CAST([Item Description] AS nvarchar(255)), ''), NULLIF(CAST([Order Type Description] AS nvarchar(255)), ''), NULLIF(CAST([Division] AS nvarchar(255)), ''), CAST([Order Type] AS nvarchar(255)))) AS [Description],
        MAX(CAST([Order Date] AS date)) AS [OrderDate],
        MAX(CAST([Requested Delivery Date] AS date)) AS [PlannedDelivery],
        MAX(CAST([Estimated Delivery Date] AS date)) AS [PromisedDate],
        MAX(CAST([Dispatch Date] AS date)) AS [ActualDelivery],
        CAST(SUM(COALESCE(CAST([Order Row Sum BCU] AS decimal(18,2)), CAST([Order Row Sum] AS decimal(18,2)), 0)) AS decimal(18,2)) AS [AmountExclVat],
        CAST(SUM(COALESCE(CAST([Order Row Sum BCU] AS decimal(18,2)), CAST([Order Row Sum] AS decimal(18,2)), 0)) AS decimal(18,2)) AS [AmountInclVat],
        MAX(COALESCE(CAST([Currency Code] AS varchar(20)), 'SEK')) AS [Currency],
        MAX(COALESCE(NULLIF(CAST([Order Status Description] AS varchar(100)), ''), CAST([Order Status] AS varchar(50)))) AS [StatusCode],
        MAX(COALESCE(NULLIF(CAST([Order Type Description] AS nvarchar(255)), ''), CAST([Order Type] AS nvarchar(255)))) AS [OrderType],
        MAX(CAST([Sales Code] AS varchar(50))) AS [SalesPerson],
        MAX(CAST([Company] AS varchar(50))) AS [CompanyCode],
        CAST(CASE
            WHEN SUM(CASE
                        WHEN COALESCE(CAST([Order Qty] AS decimal(18,4)), 0) - COALESCE(CAST([Delivered Qty] AS decimal(18,4)), 0) > 0
                        THEN COALESCE(CAST([Order Qty] AS decimal(18,4)), 0) - COALESCE(CAST([Delivered Qty] AS decimal(18,4)), 0)
                        ELSE 0
                     END) <= 0 THEN 1
            ELSE 0
        END AS bit) AS [IsClosed]
    FROM base
    GROUP BY [Order No]
),
filtered AS (
    SELECT *
    FROM grouped
    WHERE (
            @PaymentFilter = 'all'
            OR (@PaymentFilter = 'paid' AND IsClosed = 1)
            OR (@PaymentFilter = 'unpaid' AND IsClosed = 0)
      )
)
SELECT *
INTO #FilteredBiOrders
FROM filtered;

SELECT COUNT(1) AS [TotalCount]
FROM #FilteredBiOrders;

WITH numbered AS (
    SELECT *,
           ROW_NUMBER() OVER (ORDER BY {orderBy} {direction}, OrderNo DESC) AS [RowNum]
    FROM #FilteredBiOrders
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

DROP TABLE #FilteredBiOrders;";

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
                operationName: "BiOrdersRepository.GetOrdersPage");
        }

        public async Task<OrdersSummaryDto> GetOrdersSummaryAsync(string connectionString, GetOrdersQuery query)
        {
            var normalizedPaymentFilter = string.IsNullOrWhiteSpace(query.PaymentFilter)
                ? "all"
                : query.PaymentFilter.Trim().ToLowerInvariant();

            const string sql = @"
WITH base AS (
    SELECT
        [Company],
        [Order No],
        [Order Date],
        [Customer No],
        [Payer Name],
        [Item No],
        [Item Description],
        [Order Row Sum],
        [Order Row Sum BCU],
        [Order Qty],
        [Delivered Qty]
    FROM [dbo].[q_zu_bi_fsg_ord]
    WHERE [Order No] IS NOT NULL
      AND (@CompanyCode IS NULL OR [Company] = @CompanyCode)
      AND (@Search IS NULL
           OR CAST([Order No] AS varchar(50)) LIKE @Search
           OR CAST([Customer No] AS nvarchar(100)) LIKE @Search
           OR CAST([Payer Name] AS nvarchar(255)) LIKE @Search
           OR CAST([Item No] AS nvarchar(100)) LIKE @Search
           OR CAST([Item Description] AS nvarchar(255)) LIKE @Search)
      AND (@FromDate IS NULL OR CAST([Order Date] AS date) >= @FromDate)
      AND (@ToDate IS NULL OR CAST([Order Date] AS date) <= @ToDate)
),
grouped AS (
    SELECT
        CAST([Order No] AS bigint) AS [OrderNo],
        CAST(SUM(COALESCE(CAST([Order Row Sum BCU] AS decimal(18,2)), CAST([Order Row Sum] AS decimal(18,2)), 0)) AS decimal(18,2)) AS [AmountInclVat],
        CAST(CASE
            WHEN SUM(CASE
                        WHEN COALESCE(CAST([Order Qty] AS decimal(18,4)), 0) - COALESCE(CAST([Delivered Qty] AS decimal(18,4)), 0) > 0
                        THEN COALESCE(CAST([Order Qty] AS decimal(18,4)), 0) - COALESCE(CAST([Delivered Qty] AS decimal(18,4)), 0)
                        ELSE 0
                     END) <= 0 THEN 1
            ELSE 0
        END AS bit) AS [IsClosed]
    FROM base
    GROUP BY [Order No]
),
filtered AS (
    SELECT *
    FROM grouped
    WHERE (
            @PaymentFilter = 'all'
            OR (@PaymentFilter = 'paid' AND IsClosed = 1)
            OR (@PaymentFilter = 'unpaid' AND IsClosed = 0)
      )
)
SELECT
    CAST(SUM(CASE WHEN IsClosed = 1 THEN AmountInclVat ELSE 0 END) AS decimal(18,2)) AS [PaidAmountTotal],
    CAST(SUM(CASE WHEN IsClosed = 0 THEN AmountInclVat ELSE 0 END) AS decimal(18,2)) AS [UnpaidAmountTotal],
    CAST(SUM(AmountInclVat) AS decimal(18,2)) AS [GrandAmountTotal]
FROM filtered;";

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
                operationName: "BiOrdersRepository.GetOrdersSummary");

            return summary ?? new OrdersSummaryDto();
        }

        public async Task<DateTime?> GetLatestOrderDateAsync(string connectionString, int? companyCode)
        {
            const string sql = @"
SELECT MAX(TRY_CONVERT(date, [Order Date]))
FROM [dbo].[q_zu_bi_fsg_ord]
WHERE (@CompanyCode IS NULL OR CAST([Company] AS varchar(50)) = CAST(@CompanyCode AS varchar(50)));";

            return await _jeevesSqlExecutor.QueryFirstOrDefaultAsync<DateTime?>(
                connectionString,
                sql,
                new { CompanyCode = companyCode },
                operationName: "BiOrdersRepository.GetLatestOrderDate");
        }

        public async Task<OrderDeliveryInsightSummaryDto> GetOverdueDeliverySummaryAsync(string connectionString, GetOrderDeliveryInsightQuery query)
        {
            return await GetDeliveryInsightSummaryAsync(
                connectionString,
                query,
                comparisonOperator: "<",
                operationName: "BiOrdersRepository.GetOverdueDeliverySummary");
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
                operationName: "BiOrdersRepository.GetFutureDeliverySummary");
        }

        public async Task<IReadOnlyList<OrderDeliveryTimelineBucketDto>> GetFutureDeliveryTimelineAsync(string connectionString, GetDeliveryForecastQuery query)
        {
            var safeMonthsAhead = query.MonthsAhead <= 0 ? 6 : query.MonthsAhead;
            const string sql = @"
WITH base AS (
    SELECT *
    FROM [dbo].[q_zu_bi_fsg_ord]
    WHERE [Order No] IS NOT NULL
      AND (@CompanyCode IS NULL OR [Company] = @CompanyCode)
      AND (@CustomerNo IS NULL OR CAST([Customer No] AS varchar(100)) = @CustomerNo)
      AND COALESCE(CAST([Estimated Delivery Date] AS date), CAST([Requested Delivery Date] AS date)) IS NOT NULL
      AND CAST(COALESCE(CAST([Estimated Delivery Date] AS date), CAST([Requested Delivery Date] AS date)) AS date) > CAST(GETDATE() AS date)
      AND DATEFROMPARTS(
            YEAR(COALESCE(CAST([Estimated Delivery Date] AS date), CAST([Requested Delivery Date] AS date))),
            MONTH(COALESCE(CAST([Estimated Delivery Date] AS date), CAST([Requested Delivery Date] AS date))),
            1
          ) < DATEADD(MONTH, @MonthsAhead, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1))
),
grouped AS (
    SELECT
        DATEFROMPARTS(
            YEAR(COALESCE(CAST([Estimated Delivery Date] AS date), CAST([Requested Delivery Date] AS date))),
            MONTH(COALESCE(CAST([Estimated Delivery Date] AS date), CAST([Requested Delivery Date] AS date))),
            1
        ) AS [PeriodStart],
        CAST([Order No] AS bigint) AS [OrderNo],
        CAST(SUM(COALESCE(CAST([Order Row Sum BCU] AS decimal(18,2)), CAST([Order Row Sum] AS decimal(18,2)), 0)) AS decimal(18,2)) AS [AmountInclVat],
        CAST(CASE
            WHEN SUM(CASE
                        WHEN COALESCE(CAST([Order Qty] AS decimal(18,4)), 0) - COALESCE(CAST([Delivered Qty] AS decimal(18,4)), 0) > 0
                        THEN COALESCE(CAST([Order Qty] AS decimal(18,4)), 0) - COALESCE(CAST([Delivered Qty] AS decimal(18,4)), 0)
                        ELSE 0
                     END) <= 0 THEN 1
            ELSE 0
        END AS bit) AS [IsClosed]
    FROM base
    GROUP BY
        [Order No],
        DATEFROMPARTS(
            YEAR(COALESCE(CAST([Estimated Delivery Date] AS date), CAST([Requested Delivery Date] AS date))),
            MONTH(COALESCE(CAST([Estimated Delivery Date] AS date), CAST([Requested Delivery Date] AS date))),
            1
        )
)
SELECT
    [PeriodStart],
    COUNT(1) AS [OrderCount],
    CAST(ISNULL(SUM([AmountInclVat]), 0) AS decimal(18,2)) AS [AmountTotal]
FROM grouped
WHERE [IsClosed] = 0
GROUP BY [PeriodStart]
ORDER BY [PeriodStart];";

            return await _jeevesSqlExecutor.QueryAsync<OrderDeliveryTimelineBucketDto>(
                connectionString,
                sql,
                new
                {
                    CompanyCode = query.CompanyCode,
                    CustomerNo = string.IsNullOrWhiteSpace(query.CustomerNo) ? null : query.CustomerNo.Trim(),
                    MonthsAhead = safeMonthsAhead
                },
                operationName: "BiOrdersRepository.GetFutureDeliveryTimeline");
        }

        public async Task<OrderWithLinesDto?> GetOrderWithLinesAsync(string connectionString, GetOrderDetailsQuery query)
        {
            const string headerSql = @"
WITH base AS (
    SELECT *
    FROM [dbo].[q_zu_bi_fsg_ord]
    WHERE [Order No] = @OrderNo
      AND (@CompanyCode IS NULL OR [Company] = @CompanyCode)
)
SELECT
    CAST([Order No] AS bigint) AS [OrderNo],
    MAX(CAST([Order No] AS varchar(50))) AS [OrderNoAlfa],
    MAX(CAST([Customer No] AS varchar(100))) AS [CustomerNo],
    MAX(COALESCE(NULLIF(CAST([Payer Name] AS nvarchar(255)), ''), CAST([Customer No] AS nvarchar(255)))) AS [CustomerName],
    MAX(COALESCE(NULLIF(CAST([Item Description] AS nvarchar(255)), ''), NULLIF(CAST([Order Type Description] AS nvarchar(255)), ''), NULLIF(CAST([Division] AS nvarchar(255)), ''), CAST([Order Type] AS nvarchar(255)))) AS [Description],
    MAX(CAST([Order Date] AS date)) AS [OrderDate],
    MAX(CAST([Requested Delivery Date] AS date)) AS [PlannedDelivery],
    MAX(CAST([Estimated Delivery Date] AS date)) AS [PromisedDate],
    MAX(CAST([Dispatch Date] AS date)) AS [ActualDelivery],
    CAST(SUM(COALESCE(CAST([Order Row Sum BCU] AS decimal(18,2)), CAST([Order Row Sum] AS decimal(18,2)), 0)) AS decimal(18,2)) AS [AmountExclVat],
    CAST(SUM(COALESCE(CAST([Order Row Sum BCU] AS decimal(18,2)), CAST([Order Row Sum] AS decimal(18,2)), 0)) AS decimal(18,2)) AS [AmountInclVat],
    MAX(COALESCE(CAST([Currency Code] AS varchar(20)), 'SEK')) AS [Currency],
    MAX(COALESCE(NULLIF(CAST([Order Status Description] AS varchar(100)), ''), CAST([Order Status] AS varchar(50)))) AS [StatusCode],
    MAX(COALESCE(NULLIF(CAST([Order Type Description] AS nvarchar(255)), ''), CAST([Order Type] AS nvarchar(255)))) AS [OrderType],
    MAX(CAST([Sales Code] AS varchar(50))) AS [SalesPerson],
    MAX(CAST([Company] AS varchar(50))) AS [CompanyCode],
    CAST(CASE
        WHEN SUM(CASE
                    WHEN COALESCE(CAST([Order Qty] AS decimal(18,4)), 0) - COALESCE(CAST([Delivered Qty] AS decimal(18,4)), 0) > 0
                    THEN COALESCE(CAST([Order Qty] AS decimal(18,4)), 0) - COALESCE(CAST([Delivered Qty] AS decimal(18,4)), 0)
                    ELSE 0
                 END) <= 0 THEN 1
        ELSE 0
    END AS bit) AS [IsClosed]
FROM base
GROUP BY [Order No];";

            const string linesSql = @"
SELECT
    CAST([Order No] AS bigint) AS [OrderNo],
    COALESCE(CAST([Order Line No] AS int), CAST([Order Row No] AS int), 0) AS [LineNo],
    CAST([Item No] AS varchar(100)) AS [ArticleNo],
    COALESCE(NULLIF(CAST([Item Description] AS nvarchar(255)), ''), CAST([Item No] AS nvarchar(255))) AS [ArticleDescription],
    COALESCE(CAST([Order Qty] AS decimal(18,4)), 0) AS [OrderedQty],
    COALESCE(CAST([Delivered Qty] AS decimal(18,4)), 0) AS [DeliveredQty],
    CASE
        WHEN COALESCE(CAST([Order Qty] AS decimal(18,4)), 0) - COALESCE(CAST([Delivered Qty] AS decimal(18,4)), 0) > 0
        THEN COALESCE(CAST([Order Qty] AS decimal(18,4)), 0) - COALESCE(CAST([Delivered Qty] AS decimal(18,4)), 0)
        ELSE 0
    END AS [RestQty],
    CAST('' AS varchar(20)) AS [Unit],
    COALESCE(CAST([Order Row Price] AS decimal(18,2)), 0) AS [NetPrice],
    COALESCE(CAST([Order Row Sum BCU] AS decimal(18,2)), CAST([Order Row Sum] AS decimal(18,2)), 0) AS [LineAmountExclVat],
    COALESCE(CAST([Order Row Sum BCU] AS decimal(18,2)), CAST([Order Row Sum] AS decimal(18,2)), 0) AS [LineAmountInclVat],
    COALESCE(
        CAST([Order Value Discount%] AS decimal(18,2)),
        CAST([Customer Discount%] AS decimal(18,2)),
        CAST([Order Row Discount 1%] AS decimal(18,2)),
        0
    ) AS [DiscountPercent],
    COALESCE(CAST([Discount Amount] AS decimal(18,2)), CAST([Order Row Discount] AS decimal(18,2)), 0) AS [DiscountValue],
    COALESCE(CAST([Currency Code] AS varchar(20)), 'SEK') AS [Currency]
FROM [dbo].[q_zu_bi_fsg_ord]
WHERE [Order No] = @OrderNo
  AND (@CompanyCode IS NULL OR [Company] = @CompanyCode)
ORDER BY COALESCE(CAST([Order Line No] AS int), CAST([Order Row No] AS int), 0);";

            var header = await _jeevesSqlExecutor.QueryFirstOrDefaultAsync<OrderHeaderDto>(
                connectionString,
                headerSql,
                new { OrderNo = query.OrderNo, CompanyCode = query.CompanyCode },
                operationName: "BiOrdersRepository.GetOrderHeader");

            if (header == null)
            {
                return null;
            }

            var lines = await _jeevesSqlExecutor.QueryAsync<OrderLineDto>(
                connectionString,
                linesSql,
                new { OrderNo = query.OrderNo, CompanyCode = query.CompanyCode },
                operationName: "BiOrdersRepository.GetOrderLines");

            return new OrderWithLinesDto
            {
                Header = header,
                Lines = lines
            };
        }

        public async Task<IReadOnlyList<OrderCustomerOption>> GetFutureDeliveryCustomerOptionsAsync(string connectionString, GetDeliveryForecastQuery query)
        {
            var safeMonthsAhead = query.MonthsAhead <= 0 ? 6 : query.MonthsAhead;
            const string biSql = @"
SELECT
    CAST([Customer No] AS varchar(100)) AS [CustomerNo],
    MAX(COALESCE(NULLIF(CAST([Payer Name] AS nvarchar(255)), ''), CAST([Customer No] AS nvarchar(255)))) AS [CustomerName]
FROM [dbo].[q_zu_bi_fsg_ord]
WHERE [Order No] IS NOT NULL
  AND (@CompanyCode IS NULL OR [Company] = @CompanyCode)
  AND COALESCE(CAST([Estimated Delivery Date] AS date), CAST([Requested Delivery Date] AS date)) IS NOT NULL
  AND CAST(COALESCE(CAST([Estimated Delivery Date] AS date), CAST([Requested Delivery Date] AS date)) AS date) > CAST(GETDATE() AS date)
  AND DATEFROMPARTS(
        YEAR(COALESCE(CAST([Estimated Delivery Date] AS date), CAST([Requested Delivery Date] AS date))),
        MONTH(COALESCE(CAST([Estimated Delivery Date] AS date), CAST([Requested Delivery Date] AS date))),
        1
      ) < DATEADD(MONTH, @MonthsAhead, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1))
  AND (COALESCE(CAST([Order Qty] AS decimal(18,4)), 0) - COALESCE(CAST([Delivered Qty] AS decimal(18,4)), 0)) > 0
GROUP BY [Customer No]
ORDER BY [CustomerName], [CustomerNo];";

            return await _jeevesSqlExecutor.QueryAsync<OrderCustomerOption>(
                connectionString,
                biSql,
                new { CompanyCode = query.CompanyCode, MonthsAhead = safeMonthsAhead },
                operationName: "BiOrdersRepository.GetFutureDeliveryCustomerOptions");
        }

        public async Task<PagedOrdersPageResultDto> GetUpcomingOrdersPageAsync(string connectionString, GetDeliveryForecastQuery query)
        {
            var safeMonthsAhead = query.MonthsAhead <= 0 ? 6 : query.MonthsAhead;
            var safePage = query.Page <= 0 ? 1 : query.Page;
            var safePageSize = query.PageSize <= 0 ? 25 : query.PageSize;
            var startRow = ((safePage - 1) * safePageSize) + 1;
            var endRow = safePage * safePageSize;

            var biSql = @"
WITH base AS (
    SELECT *
    FROM [dbo].[q_zu_bi_fsg_ord]
    WHERE [Order No] IS NOT NULL
      AND (@CompanyCode IS NULL OR [Company] = @CompanyCode)
      AND (@CustomerNo IS NULL OR CAST([Customer No] AS varchar(100)) = @CustomerNo)
      AND COALESCE(CAST([Estimated Delivery Date] AS date), CAST([Requested Delivery Date] AS date)) IS NOT NULL
      AND CAST(COALESCE(CAST([Estimated Delivery Date] AS date), CAST([Requested Delivery Date] AS date)) AS date) > CAST(GETDATE() AS date)
      AND DATEFROMPARTS(
            YEAR(COALESCE(CAST([Estimated Delivery Date] AS date), CAST([Requested Delivery Date] AS date))),
            MONTH(COALESCE(CAST([Estimated Delivery Date] AS date), CAST([Requested Delivery Date] AS date))),
            1
          ) < DATEADD(MONTH, @MonthsAhead, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1))
),
grouped AS (
    SELECT
        CAST([Order No] AS bigint) AS [OrderNo],
        MAX(CAST([Order No] AS varchar(50))) AS [OrderNoAlfa],
        MAX(CAST([Customer No] AS varchar(100))) AS [CustomerNo],
        MAX(COALESCE(NULLIF(CAST([Payer Name] AS nvarchar(255)), ''), CAST([Customer No] AS nvarchar(255)))) AS [CustomerName],
        MAX(COALESCE(NULLIF(CAST([Item Description] AS nvarchar(255)), ''), NULLIF(CAST([Order Type Description] AS nvarchar(255)), ''), NULLIF(CAST([Division] AS nvarchar(255)), ''), CAST([Order Type] AS nvarchar(255)))) AS [Description],
        MAX(CAST([Order Date] AS date)) AS [OrderDate],
        MAX(CAST([Requested Delivery Date] AS date)) AS [PlannedDelivery],
        MAX(CAST([Estimated Delivery Date] AS date)) AS [PromisedDate],
        MAX(CAST([Dispatch Date] AS date)) AS [ActualDelivery],
        CAST(SUM(COALESCE(CAST([Order Row Sum BCU] AS decimal(18,2)), CAST([Order Row Sum] AS decimal(18,2)), 0)) AS decimal(18,2)) AS [AmountExclVat],
        CAST(SUM(COALESCE(CAST([Order Row Sum BCU] AS decimal(18,2)), CAST([Order Row Sum] AS decimal(18,2)), 0)) AS decimal(18,2)) AS [AmountInclVat],
        MAX(COALESCE(CAST([Currency Code] AS varchar(20)), 'SEK')) AS [Currency],
        MAX(COALESCE(NULLIF(CAST([Order Status Description] AS varchar(100)), ''), CAST([Order Status] AS varchar(50)))) AS [StatusCode],
        MAX(COALESCE(NULLIF(CAST([Order Type Description] AS nvarchar(255)), ''), CAST([Order Type] AS nvarchar(255)))) AS [OrderType],
        MAX(CAST([Sales Code] AS varchar(50))) AS [SalesPerson],
        MAX(CAST([Company] AS varchar(50))) AS [CompanyCode],
        CAST(CASE
            WHEN SUM(CASE
                        WHEN COALESCE(CAST([Order Qty] AS decimal(18,4)), 0) - COALESCE(CAST([Delivered Qty] AS decimal(18,4)), 0) > 0
                        THEN COALESCE(CAST([Order Qty] AS decimal(18,4)), 0) - COALESCE(CAST([Delivered Qty] AS decimal(18,4)), 0)
                        ELSE 0
                     END) <= 0 THEN 1
            ELSE 0
        END AS bit) AS [IsClosed]
    FROM base
    GROUP BY [Order No]
)
SELECT *
INTO #UpcomingOrders
FROM grouped
WHERE IsClosed = 0;

SELECT COUNT(1) AS [TotalCount]
FROM #UpcomingOrders;

WITH numbered AS (
    SELECT *,
           ROW_NUMBER() OVER (ORDER BY COALESCE([PromisedDate], [PlannedDelivery], [OrderDate]) ASC, [OrderNo] DESC) AS [RowNum]
    FROM #UpcomingOrders
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

DROP TABLE #UpcomingOrders;";

            return await _jeevesSqlExecutor.WithConnectionAsync(
                connectionString,
                async connection =>
                {
                    using var multi = await connection.QueryMultipleAsync(
                        biSql,
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
                operationName: "BiOrdersRepository.GetUpcomingOrdersPage");
        }

        private async Task<OrderDeliveryInsightSummaryDto> GetDeliveryInsightSummaryAsync(
            string connectionString,
            GetOrderDeliveryInsightQuery query,
            string comparisonOperator,
            string operationName)
        {
            var sql = $@"
WITH grouped AS (
    SELECT
        TRY_CONVERT(bigint, [Order No]) AS [OrderNo],
        COALESCE(
            MAX(TRY_CONVERT(date, [Estimated Delivery Date])),
            MAX(TRY_CONVERT(date, [Requested Delivery Date]))
        ) AS [DeliveryDate],
        CAST(SUM(COALESCE(TRY_CONVERT(decimal(18,2), [Order Row Sum BCU]), TRY_CONVERT(decimal(18,2), [Order Row Sum]), 0)) AS decimal(18,2)) AS [AmountInclVat],
        CAST(CASE
            WHEN SUM(CASE
                        WHEN COALESCE(TRY_CONVERT(decimal(18,4), [Order Qty]), 0) - COALESCE(TRY_CONVERT(decimal(18,4), [Delivered Qty]), 0) > 0
                        THEN COALESCE(TRY_CONVERT(decimal(18,4), [Order Qty]), 0) - COALESCE(TRY_CONVERT(decimal(18,4), [Delivered Qty]), 0)
                        ELSE 0
                     END) <= 0 THEN 1
            ELSE 0
        END AS bit) AS [IsClosed]
    FROM [dbo].[q_zu_bi_fsg_ord]
    WHERE TRY_CONVERT(bigint, [Order No]) IS NOT NULL
      AND (@CompanyCode IS NULL OR CAST([Company] AS varchar(50)) = CAST(@CompanyCode AS varchar(50)))
      AND (@CustomerNo IS NULL OR CAST([Customer No] AS varchar(100)) = @CustomerNo)
    GROUP BY TRY_CONVERT(bigint, [Order No])
)
SELECT
    COUNT(1) AS [OrderCount],
    CAST(ISNULL(SUM([AmountInclVat]), 0) AS decimal(18,2)) AS [AmountTotal],
    MIN([DeliveryDate]) AS [EarliestDate],
    MAX([DeliveryDate]) AS [LatestDate]
FROM grouped
WHERE [IsClosed] = 0
  AND [DeliveryDate] IS NOT NULL
  AND CAST([DeliveryDate] AS date) {comparisonOperator} CAST(GETDATE() AS date);";

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

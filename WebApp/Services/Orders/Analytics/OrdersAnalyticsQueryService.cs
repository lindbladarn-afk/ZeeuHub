using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebApp.Models.Dashboard;
using WebApp.Services.Application;
using Repository.Execution;

namespace WebApp.Services.Orders;

// Executes the raw Jeeves queries used by the orders analytics dashboard.
// The source selection is kept here so the dashboard can stay agnostic to legacy vs BI storage.
public sealed class OrdersAnalyticsQueryService : IOrdersAnalyticsQueryService
{
    private const string LegacyOrderTotalsSql = @"
SELECT
    oh.OrderNr       AS OrderNumber,
    oh.OrderNrAlfa   AS OrderNumberText,
    oh.OrdDatum      AS OrderDate,
    SUM(orp.vb_RadVardeInklMoms) AS AmountInclVat
FROM dbo.oh oh
INNER JOIN dbo.orp orp
    ON orp.OrderNr = oh.OrderNr
   AND orp.ForetagKod = oh.ForetagKod
WHERE oh.OrdDatum IS NOT NULL
  AND (@FromDate IS NULL OR oh.OrdDatum >= @FromDate)
  AND (@CompanyCode IS NULL OR oh.ForetagKod = @CompanyCode)
GROUP BY oh.OrderNr, oh.OrderNrAlfa, oh.OrdDatum;";

    private const string BiOrderTotalsSql = @"
SELECT
    CAST([Order No] AS bigint) AS OrderNumber,
    CAST([Order No] AS varchar(50)) AS OrderNumberText,
    CAST([Order Date] AS datetime) AS OrderDate,
    CAST(SUM(COALESCE(CAST([Order Row Sum BCU] AS decimal(18,2)), CAST([Order Row Sum] AS decimal(18,2)), 0)) AS decimal(18,2)) AS AmountInclVat
FROM [dbo].[q_zu_bi_fsg_ord]
WHERE [Order Date] IS NOT NULL
  AND (@FromDate IS NULL OR [Order Date] >= @FromDate)
  AND (@CompanyCode IS NULL OR [Company] = @CompanyCode)
GROUP BY [Order No], [Order Date];";

    private const string LegacyTopSellersSql = @"
SELECT TOP(@Take)
    orp.ArtNr AS ArticleNo,
    COALESCE(orp.OrdArtBeskr, orp.ArtBeskr, orp.ArtNr) AS ArticleDescription,
    SUM(orp.vb_RadVardeInklMoms) AS Revenue,
    SUM(orp.OrdAntal) AS Quantity
FROM dbo.orp orp
INNER JOIN dbo.oh oh
    ON oh.OrderNr = orp.OrderNr
   AND oh.ForetagKod = orp.ForetagKod
WHERE (@CompanyCode IS NULL OR orp.ForetagKod = @CompanyCode)
  AND (@FromDate IS NULL OR oh.OrdDatum >= @FromDate)
GROUP BY orp.ArtNr, COALESCE(orp.OrdArtBeskr, orp.ArtBeskr, orp.ArtNr)
ORDER BY SUM(orp.vb_RadVardeInklMoms) DESC;";

    private const string BiTopSellersSql = @"
SELECT TOP(@Take)
    COALESCE([Item No], '') AS ArticleNo,
    COALESCE(NULLIF([Item Description], ''), [Item No], '-') AS ArticleDescription,
    CAST(SUM(COALESCE(CAST([Order Row Sum BCU] AS decimal(18,2)), CAST([Order Row Sum] AS decimal(18,2)), 0)) AS decimal(18,2)) AS Revenue,
    CAST(SUM(COALESCE([Order Qty], 0)) AS decimal(18,2)) AS Quantity
FROM [dbo].[q_zu_bi_fsg_ord]
WHERE (@CompanyCode IS NULL OR [Company] = @CompanyCode)
  AND (@FromDate IS NULL OR [Order Date] >= @FromDate)
  AND COALESCE([Item No], '') <> ''
GROUP BY [Item No], COALESCE(NULLIF([Item Description], ''), [Item No], '-')
ORDER BY SUM(COALESCE(CAST([Order Row Sum BCU] AS decimal(18,2)), CAST([Order Row Sum] AS decimal(18,2)), 0)) DESC;";

    private const string LegacyLatestOrderDateSql = @"
SELECT MAX(oh.OrdDatum)
FROM dbo.oh oh
WHERE (@CompanyCode IS NULL OR oh.ForetagKod = @CompanyCode);";

    private const string BiLatestOrderDateSql = @"
SELECT MAX([Order Date])
FROM [dbo].[q_zu_bi_fsg_ord]
WHERE (@CompanyCode IS NULL OR [Company] = @CompanyCode);";

    private readonly IJeevesConnectionResolver _jeevesConnectionResolver;
    private readonly IOrderSourceSelector _orderSourceSelector;
    private readonly IJeevesSqlExecutor _jeevesSqlExecutor;

    public OrdersAnalyticsQueryService(
        IJeevesConnectionResolver jeevesConnectionResolver,
        IOrderSourceSelector orderSourceSelector,
        IJeevesSqlExecutor jeevesSqlExecutor)
    {
        _jeevesConnectionResolver = jeevesConnectionResolver;
        _orderSourceSelector = orderSourceSelector;
        _jeevesSqlExecutor = jeevesSqlExecutor;
    }

    public async Task<IReadOnlyList<OrderTotalPoint>> GetOrderTotalsAsync(string connectionString, int? companyCode, DateTime? fromDate = null)
    {
        var selected = await SelectSourceAsync(connectionString);
        if (string.IsNullOrWhiteSpace(selected.ConnectionString))
        {
            return Array.Empty<OrderTotalPoint>();
        }

        return await _jeevesSqlExecutor.QueryAsync<OrderTotalPoint>(
            selected.ConnectionString,
            selected.Source == OrderDataSource.Bi ? BiOrderTotalsSql : LegacyOrderTotalsSql,
            new { CompanyCode = companyCode, FromDate = fromDate?.Date },
            operationName: selected.Source == OrderDataSource.Bi
                ? "OrdersAnalyticsQueryService.GetOrderTotals.Bi"
                : "OrdersAnalyticsQueryService.GetOrderTotals");
    }

    public async Task<IReadOnlyList<TopSellerItem>> GetTopSellersAsync(string connectionString, int? companyCode, int take = 6, DateTime? fromDate = null)
    {
        var selected = await SelectSourceAsync(connectionString);
        if (string.IsNullOrWhiteSpace(selected.ConnectionString))
        {
            return Array.Empty<TopSellerItem>();
        }

        return await _jeevesSqlExecutor.QueryAsync<TopSellerItem>(
            selected.ConnectionString,
            selected.Source == OrderDataSource.Bi ? BiTopSellersSql : LegacyTopSellersSql,
            new { Take = take, CompanyCode = companyCode, FromDate = fromDate?.Date },
            operationName: selected.Source == OrderDataSource.Bi
                ? "OrdersAnalyticsQueryService.GetTopSellers.Bi"
                : "OrdersAnalyticsQueryService.GetTopSellers");
    }

    public async Task<DateTime?> GetLatestOrderDateAsync(string connectionString, int? companyCode)
    {
        var selected = await SelectSourceAsync(connectionString);
        if (string.IsNullOrWhiteSpace(selected.ConnectionString))
        {
            return null;
        }

        return await _jeevesSqlExecutor.ExecuteScalarAsync<DateTime?>(
            selected.ConnectionString,
            selected.Source == OrderDataSource.Bi ? BiLatestOrderDateSql : LegacyLatestOrderDateSql,
            new { CompanyCode = companyCode },
            operationName: selected.Source == OrderDataSource.Bi
                ? "OrdersAnalyticsQueryService.GetLatestOrderDate.Bi"
                : "OrdersAnalyticsQueryService.GetLatestOrderDate");
    }

    private async Task<SelectedOrderSource> SelectSourceAsync(string connectionString)
    {
        var effectiveConnectionString = string.IsNullOrWhiteSpace(connectionString)
            ? _jeevesConnectionResolver.ResolveConnectionString()
            : connectionString;

        if (string.IsNullOrWhiteSpace(effectiveConnectionString))
        {
            return new SelectedOrderSource();
        }

        var source = await _orderSourceSelector.SelectAsync(effectiveConnectionString);
        return new SelectedOrderSource
        {
            ConnectionString = effectiveConnectionString,
            Source = source
        };
    }

    private sealed class SelectedOrderSource
    {
        public string ConnectionString { get; set; } = string.Empty;
        public OrderDataSource Source { get; set; }
    }
}

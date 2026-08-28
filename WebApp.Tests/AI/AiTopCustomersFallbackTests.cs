// Verifies deterministic, read-only SQL for customer revenue rankings.
using System.Reflection;
using WebApp.Services.Application.AI;

namespace WebApp.Tests;

public sealed class AiTopCustomersFallbackTests
{
    private const string JeevesSalesSchema = """
        AVAILABLE TABLES & COLUMNS (SQL Server):
        - dbo.q_zu_bi_fsg (Customer No nvarchar NULL, Customer nvarchar NULL, Invoice Date datetime NULL, Qty decimal NULL, Invoice Row SUM money NULL, Jeeves Company smallint NULL)
        """;

    [Fact]
    public void RevenueRanking_PreservesMultiWordColumnsAndAvoidsSelfJoin()
    {
        var sql = Assert.Single(BuildCandidates(
            "visa mina 5 största kunder i omsättning",
            JeevesSalesSchema));

        Assert.Contains("SELECT TOP (5)", sql, StringComparison.Ordinal);
        Assert.Contains("sf.[Customer No] AS [CustomerID]", sql, StringComparison.Ordinal);
        Assert.Contains("sf.[Customer] AS [CustomerName]", sql, StringComparison.Ordinal);
        Assert.Contains(
            "SUM(CAST(sf.[Invoice Row SUM] AS decimal(18,2))) AS [TotalOmsatt]",
            sql,
            StringComparison.Ordinal);
        Assert.Contains("FROM [dbo].[q_zu_bi_fsg] sf", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("JOIN [dbo].[q_zu_bi_fsg]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void SwedishNumberAndCurrentYear_AreAppliedToRanking()
    {
        var sql = Assert.Single(BuildCandidates(
            "Visa de fem största kunderna i år",
            JeevesSalesSchema));

        Assert.Contains("SELECT TOP (5)", sql, StringComparison.Ordinal);
        Assert.Contains("sf.[Invoice Date] >= DATEFROMPARTS(YEAR(GETDATE()), 1, 1)", sql, StringComparison.Ordinal);
        Assert.Contains("sf.[Invoice Date] < DATEADD(day, 1, CAST(GETDATE() AS date))", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedRanking_PassesReadOnlyDatabasePolicy()
    {
        var sql = Assert.Single(BuildCandidates(
            "visa mina 5 största kunder i omsättning",
            JeevesSalesSchema));

        var policy = new AiSqlSecurityPolicy().Validate(sql);

        Assert.True(policy.Success, policy.Error);
    }

    [Fact]
    public void OrderValueThreshold_UsesOrderMeasureAndHavingFilter()
    {
        const string orderSchema = """
            AVAILABLE TABLES & COLUMNS (SQL Server):
            - dbo.q_zu_bi_fsg_ord (Customer No nvarchar NULL, Customer nvarchar NULL, Order Value money NULL)
            """;

        var sql = Assert.Single(BuildCandidates(
            "Visa kunder som beställt över 10 000 kr",
            orderSchema));

        Assert.DoesNotContain("TOP (", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SUM(CAST(sf.[Order Value] AS decimal(18,2))) AS [TotalOrdervarde]", sql, StringComparison.Ordinal);
        Assert.Contains("HAVING SUM(CAST(sf.[Order Value] AS decimal(18,2))) > 10000", sql, StringComparison.Ordinal);
        Assert.Contains("AS [CustomerName]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void MonthlyRevenue_UsesInvoiceFactAndCalendarMonthLabels()
    {
        var sql = Assert.Single(BuildMonthlyRevenueCandidates(
            "Visa omsättning per månad i år",
            JeevesSalesSchema));

        Assert.Contains("FROM [dbo].[q_zu_bi_fsg] sf", sql, StringComparison.Ordinal);
        Assert.Contains("CONVERT(char(7), sf.[Invoice Date], 120) AS [Month]", sql, StringComparison.Ordinal);
        Assert.Contains("SUM(CAST(sf.[Invoice Row SUM] AS decimal(18,2))) AS [TotalOmsatt]", sql, StringComparison.Ordinal);
        Assert.Contains("DATEFROMPARTS(YEAR(GETDATE()), 1, 1)", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CustomerName", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void MonthlyRevenueWithoutPeriod_UsesCurrentYearToDateByDefault()
    {
        var sql = Assert.Single(BuildMonthlyRevenueCandidates(
            "Bryt ned omsättningen per månad",
            JeevesSalesSchema));

        Assert.Contains("sf.[Invoice Date] >= DATEFROMPARTS(YEAR(GETDATE()), 1, 1)", sql, StringComparison.Ordinal);
        Assert.Contains("sf.[Invoice Date] < DATEADD(day, 1, CAST(GETDATE() AS date))", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void YearToDateComparison_RecognizesJeevesInvoiceAmountColumn()
    {
        const string jeevesSchema = """
            AVAILABLE TABLES & COLUMNS (SQL Server):
            - dbo.ft (FaktDat datetime NULL, FaktRadSumma money NULL)
            """;

        var sql = Assert.Single(BuildYearToDateCandidates(
            "visa årets omsättning mot förra årets omsättning på samma månader",
            jeevesSchema));

        Assert.Contains("[FaktDat]", sql, StringComparison.Ordinal);
        Assert.Contains("[FaktRadSumma]", sql, StringComparison.Ordinal);
        Assert.Contains("AS [CurrentYearToDate]", sql, StringComparison.Ordinal);
        Assert.Contains("AS [PreviousYearToDate]", sql, StringComparison.Ordinal);
        Assert.Contains("AS [Difference]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentYearRevenue_StopsAtToday()
    {
        var sql = Assert.Single(BuildCurrentYearCandidates(
            "Visa årets omsättning",
            JeevesSalesSchema));

        Assert.Contains("sf.[Invoice Date] < DATEADD(day, 1, CAST(GETDATE() AS date))", sql, StringComparison.Ordinal);
        Assert.Contains("AS [TotalOmsatt]", sql, StringComparison.Ordinal);
    }

    private static List<string> BuildCandidates(string question, string schema)
    {
        var method = typeof(AiDbChatOrchestrator).GetMethod(
            "BuildTopCustomersFallbackSqlCandidates",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [question, schema]);
        return Assert.IsType<List<string>>(result);
    }

    private static List<string> BuildMonthlyRevenueCandidates(string question, string schema)
    {
        var method = typeof(AiDbChatOrchestrator).GetMethod(
            "BuildMonthlyRevenueFallbackSqlCandidates",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [question, schema]);
        return Assert.IsType<List<string>>(result);
    }

    private static List<string> BuildYearToDateCandidates(string question, string schema)
    {
        var method = typeof(AiDbChatOrchestrator).GetMethod(
            "BuildYearToDateRevenueComparisonSqlCandidates",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [question, schema]);
        return Assert.IsType<List<string>>(result);
    }

    private static List<string> BuildCurrentYearCandidates(string question, string schema)
    {
        var method = typeof(AiDbChatOrchestrator).GetMethod(
            "BuildYearToDateRevenueSqlCandidates",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [question, schema]);
        return Assert.IsType<List<string>>(result);
    }
}

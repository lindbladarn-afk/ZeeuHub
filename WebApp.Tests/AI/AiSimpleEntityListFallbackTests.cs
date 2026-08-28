// Verifies deterministic read-only SQL for simple customer, item, and supplier lists.
using System.Reflection;
using WebApp.Services.Application.AI;

namespace WebApp.Tests;

public sealed class AiSimpleEntityListFallbackTests
{
    private const string JeevesSchema = """
        AVAILABLE TABLES AND COLUMNS:
        - dbo.fr (ForetagKod smallint NOT NULL, FtgNr nvarchar NOT NULL, FtgNamn nvarchar NULL, FtgKundKod char NULL, FtgLevKod char NULL, FtgPostAdr3 nvarchar NULL)
        - dbo.ar (ForetagKod smallint NOT NULL, ArtBeskr nvarchar NULL, ArtKat nvarchar NULL, ArtNr nvarchar NOT NULL)
        """;

    [Fact]
    public void CustomerList_UsesCustomerRegisterWithoutForcingCompanyFilter()
    {
        var sql = BuildSql("Visa mina kunder");

        Assert.Contains("FROM [dbo].[fr]", sql, StringComparison.Ordinal);
        Assert.Contains("[FtgKundKod] = '1'", sql, StringComparison.Ordinal);
        Assert.Contains("[FtgNr] AS [CustomerNumber]", sql, StringComparison.Ordinal);
        Assert.Contains("[FtgNamn] AS [CustomerName]", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("[ForetagKod] =", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ItemList_UsesItemRegisterAndKnownItemColumns()
    {
        var sql = BuildSql("Visa alla artiklar");

        Assert.Contains("FROM [dbo].[ar]", sql, StringComparison.Ordinal);
        Assert.Contains("[ArtNr] AS [ItemNumber]", sql, StringComparison.Ordinal);
        Assert.Contains("[ArtBeskr] AS [ItemDescription]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void SupplierList_UsesSupplierRowsFromCompanyRegister()
    {
        var sql = BuildSql("Visa leverantörer");

        Assert.Contains("FROM [dbo].[fr]", sql, StringComparison.Ordinal);
        Assert.Contains("[FtgLevKod] = '1'", sql, StringComparison.Ordinal);
        Assert.Contains("[FtgNr] AS [SupplierNumber]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalyticalCustomerQuestion_IsLeftToThePlanningFlow()
    {
        Assert.Null(InvokeBuilder("Visa de fem största kunderna i år"));
    }

    [Fact]
    public void FilteredCustomerQuestion_IsLeftToThePlanningFlow()
    {
        Assert.Null(InvokeBuilder("Visa kunder i Göteborg"));
    }

    [Fact]
    public void RequestedCustomerColumns_StillUseTheDeterministicList()
    {
        var sql = BuildSql("Visa kundnummer och kundnamn");

        Assert.Contains("[FtgNr] AS [CustomerNumber]", sql, StringComparison.Ordinal);
        Assert.Contains("[FtgNamn] AS [CustomerName]", sql, StringComparison.Ordinal);
    }

    private static string BuildSql(string question)
    {
        var result = InvokeBuilder(question);
        Assert.NotNull(result);

        var sql = result.GetType()
            .GetProperty("Sql", BindingFlags.Instance | BindingFlags.Public)?
            .GetValue(result) as string;
        return Assert.IsType<string>(sql);
    }

    private static object? InvokeBuilder(string question)
    {
        var method = typeof(AiDbChatOrchestrator).GetMethod(
            "BuildSimpleEntityListFallback",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        return method.Invoke(null, [question, JeevesSchema]);
    }
}

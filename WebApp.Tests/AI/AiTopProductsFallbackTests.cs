// Verifies deterministic SQL fallbacks for product rankings.
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace WebApp.Tests;

public sealed class AiTopProductsFallbackTests
{
    [Fact]
    public void TopProductsFallback_ExcludesZeroRows()
    {
        var method = typeof(WebApp.Services.Application.AI.AiDbChatOrchestrator)
            .GetMethod("BuildTopProductsFallbackSqlCandidates", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var schemaText = """
- dbo.salesfact (ProductID, Quantity, CompanyCode)
""";

        var result = method!.Invoke(null, new object[] { "vilken artikel har vi sålt mest av", schemaText });
        var sqlCandidates = Assert.IsAssignableFrom<List<string>>(result);

        Assert.NotEmpty(sqlCandidates);
        Assert.Contains("HAVING SUM(CAST(sf.[Quantity] AS int)) > 0", sqlCandidates[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TopProductsFallback_PreservesMultiWordColumnsAndCurrentYear()
    {
        var method = typeof(WebApp.Services.Application.AI.AiDbChatOrchestrator)
            .GetMethod("BuildTopProductsFallbackSqlCandidates", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var schemaText = """
            - dbo.q_zu_bi_fsg (Item No nvarchar NULL, Item Description nvarchar NULL, Invoice Date datetime NULL, Qty decimal NULL, Invoice Row SUM money NULL)
            """;

        var result = method!.Invoke(
            null,
            ["visa fem största produkterna i antal i år", schemaText]);
        var sql = Assert.Single(Assert.IsAssignableFrom<List<string>>(result));

        Assert.Contains("SELECT TOP (5)", sql, StringComparison.Ordinal);
        Assert.Contains("sf.[Item No] AS [ProductID]", sql, StringComparison.Ordinal);
        Assert.Contains("sf.[Item Description] AS [ProductName]", sql, StringComparison.Ordinal);
        Assert.Contains("sf.[Invoice Date] >= DATEFROMPARTS", sql, StringComparison.Ordinal);
    }
}

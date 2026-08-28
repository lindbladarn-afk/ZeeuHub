// Verifies that year-to-date comparisons keep the direction from the SQL result.
using System.Reflection;
using WebApp.Models.AI;
using WebApp.Services.Application.AI;

namespace WebApp.Tests;

public sealed class AiYearToDateComparisonTests
{
    [Fact]
    public void DeterministicSummary_WhenCurrentYearIsLower_StatesLowerRevenue()
    {
        var result = new SqlQueryResult
        {
            Success = true,
            RowCount = 1
        };
        result.Columns.AddRange(["CurrentYearToDate", "PreviousYearToDate", "Difference"]);
        result.Rows.Add([56567993m, 64849818m, -8281825m]);

        var summary = BuildSummary(result);

        Assert.Contains("lägre i år", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("högre i år", summary, StringComparison.Ordinal);
        Assert.Contains("8 281 825,00 kr", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void DeterministicSummary_AcceptsCanonicalAgentAliases()
    {
        var result = new SqlQueryResult
        {
            Success = true,
            RowCount = 1
        };
        result.Columns.AddRange(["CurrentPeriod", "PreviousPeriod", "Difference"]);
        result.Rows.Add([110m, 100m, 10m]);

        var summary = BuildSummary(result);

        Assert.Contains("högre i år", summary, StringComparison.Ordinal);
        Assert.Contains("10,00 kr", summary, StringComparison.Ordinal);
    }

    private static string BuildSummary(SqlQueryResult result)
    {
        var method = typeof(AiDbChatOrchestrator).GetMethod(
            "BuildDeterministicYearToDateComparisonAnswer",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return Assert.IsType<string>(method.Invoke(null, [result]));
    }
}

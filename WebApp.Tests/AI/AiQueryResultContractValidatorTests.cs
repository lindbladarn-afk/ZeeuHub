// Verifies that executed SQL results satisfy the semantic roles promised by the query plan.
using WebApp.Models.AI;
using WebApp.Services.Application.AI;

namespace WebApp.Tests;

public sealed class AiQueryResultContractValidatorTests
{
    [Fact]
    public void Comparison_WithAllRequiredRoles_IsValid()
    {
        var query = Result(
            ["CurrentYearToDate", "PreviousYearToDate", "Difference"],
            [100m, 90m, 10m]);
        var plan = ComparisonPlan();

        var validation = AiQueryResultContractValidator.Validate(query, plan);

        Assert.True(validation.Success, validation.Error);
    }

    [Fact]
    public void Comparison_MissingPreviousPeriod_RequiresRepair()
    {
        var query = Result(["CurrentYearToDate", "Difference"], [100m, 10m]);
        var plan = ComparisonPlan();

        var validation = AiQueryResultContractValidator.Validate(query, plan);

        Assert.False(validation.Success);
        Assert.Contains("previous_period", validation.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void MonthlyTrend_RequiresTimeAndMetricColumns()
    {
        var query = Result(["Month", "TotalOmsatt"], ["2026-01", 100m]);
        var plan = new AiQueryPlan
        {
            Intent = "trend",
            Metric = "net_revenue",
            TimeGrain = "month",
            ResultContract = new AiQueryResultContract
            {
                Shape = "series",
                RequiredRoles = ["time", "metric"]
            }
        };

        var validation = AiQueryResultContractValidator.Validate(query, plan);

        Assert.True(validation.Success, validation.Error);
    }

    [Fact]
    public void Aggregate_WithDimension_RequiresBothDimensionAndMetric()
    {
        var query = Result(["CustomerName"], ["COOP"]);
        var plan = new AiSemanticCatalog().CreateFallbackPlan("Visa omsättning per kund");

        var validation = AiQueryResultContractValidator.Validate(query, plan);

        Assert.False(validation.Success);
        Assert.Contains("metric", validation.Error, StringComparison.Ordinal);
    }

    private static AiQueryPlan ComparisonPlan() =>
        new()
        {
            Intent = "comparison",
            Metric = "net_revenue",
            Comparison = "current_vs_previous_same_period",
            ResultContract = new AiQueryResultContract
            {
                Shape = "single_row",
                RequiredRoles = ["current_period", "previous_period", "difference"]
            }
        };

    private static SqlQueryResult Result(IReadOnlyCollection<string> columns, IReadOnlyCollection<object?> row)
    {
        var result = new SqlQueryResult
        {
            Success = true,
            RowCount = 1
        };
        result.Columns.AddRange(columns);
        result.Rows.Add(row.ToList());
        return result;
    }
}

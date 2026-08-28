// Protects the structured query-plan contract returned by the model.
using WebApp.Services.Application.AI;

namespace WebApp.Tests;

public sealed class AiStructuredQueryPlanTests
{
    [Fact]
    public void Parser_ReadsPlanAndSqlFromStrictJson()
    {
        const string json = """
            {
              "plan": {
                "intent": "ranking",
                "metric": "net_revenue",
                "dimensions": ["customer"],
                "filters": [],
                "period": "current_year",
                "comparison": "none",
                "time_grain": null,
                "result_contract": {
                  "shape": "table",
                  "required_roles": ["dimension", "metric"],
                  "preferred_visualization": "bar"
                },
                "sort": "descending",
                "limit": 5,
                "assumptions": []
              },
              "sql": "SELECT TOP (5) CustomerName, SUM(Amount) AS Revenue FROM dbo.Sales GROUP BY CustomerName",
              "requires_clarification": false,
              "reason": ""
            }
            """;

        var parsed = AiSqlResponseParser.TryParseStructuredQueryResponse(json, out var response);

        Assert.True(parsed);
        Assert.Equal("ranking", response.Plan?.Intent);
        Assert.Equal("net_revenue", response.Plan?.Metric);
        Assert.Equal(5, response.Plan?.Limit);
        Assert.Equal(["dimension", "metric"], response.Plan?.ResultContract.RequiredRoles);
        Assert.StartsWith("SELECT", response.Sql, StringComparison.OrdinalIgnoreCase);
    }
}

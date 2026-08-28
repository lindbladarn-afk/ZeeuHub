using System.Collections.Concurrent;
using System.Threading.Tasks;
using Repository.Execution;

namespace WebApp.Services.Orders;

// Centralizes the schema check so both repository routing and analytics use the same source decision.
public sealed class OrderSourceSelector : IOrderSourceSelector
{
    private const string DetectSourceSql = @"
SELECT
    CASE
        WHEN OBJECT_ID(N'[dbo].[q_zu_bi_fsg_ord]', N'V') IS NOT NULL
          OR OBJECT_ID(N'[dbo].[q_zu_bi_fsg_ord]', N'U') IS NOT NULL
            THEN CAST(1 AS bit)
        ELSE CAST(0 AS bit)
    END AS HasBiFact;";

    private static readonly ConcurrentDictionary<string, OrderDataSource> SelectionCache = new();
    private readonly IJeevesSqlExecutor _jeevesSqlExecutor;

    public OrderSourceSelector(IJeevesSqlExecutor jeevesSqlExecutor)
    {
        _jeevesSqlExecutor = jeevesSqlExecutor;
    }

    public async Task<OrderDataSource> SelectAsync(string connectionString)
    {
        var cacheKey = $"orders-source:{connectionString.GetHashCode()}";
        if (SelectionCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var availability = await _jeevesSqlExecutor.QueryFirstOrDefaultAsync<OrderSourceAvailabilityRow>(
            connectionString,
            DetectSourceSql,
            operationName: "OrderSourceSelector.Select");

        var selected = availability?.HasBiFact == true
            ? OrderDataSource.Bi
            : OrderDataSource.Legacy;

        SelectionCache[cacheKey] = selected;
        return selected;
    }

    private sealed class OrderSourceAvailabilityRow
    {
        public bool HasBiFact { get; set; }
    }
}

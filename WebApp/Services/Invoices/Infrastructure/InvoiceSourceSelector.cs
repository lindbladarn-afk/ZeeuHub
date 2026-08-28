using System.Collections.Concurrent;
using System.Threading.Tasks;
using Repository.Execution;

namespace WebApp.Services.Invoices
{
    public class InvoiceSourceSelector : IInvoiceSourceSelector
    {
        private const string DetectSourceSql = @"
SELECT
    CASE
        WHEN OBJECT_ID(N'[dbo].[fh]', N'U') IS NOT NULL
            THEN CAST(1 AS bit)
        ELSE CAST(0 AS bit)
    END AS HasLegacyLedger,
    CASE
        WHEN OBJECT_ID(N'[dbo].[q_zu_bi_fsg]', N'V') IS NOT NULL
          OR OBJECT_ID(N'[dbo].[q_zu_bi_fsg]', N'U') IS NOT NULL
            THEN CAST(1 AS bit)
        ELSE CAST(0 AS bit)
    END AS HasBiFact;";

        private static readonly ConcurrentDictionary<string, InvoiceDataSource> SelectionCache = new();
        private readonly IJeevesSqlExecutor _jeevesSqlExecutor;

        public InvoiceSourceSelector(IJeevesSqlExecutor jeevesSqlExecutor)
        {
            _jeevesSqlExecutor = jeevesSqlExecutor;
        }

        public async Task<InvoiceDataSource> SelectAsync(string connectionString)
        {
            var cacheKey = $"invoices-source:{connectionString.GetHashCode()}";
            if (SelectionCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var availability = await _jeevesSqlExecutor.QueryFirstOrDefaultAsync<InvoiceSourceAvailabilityRow>(
                connectionString,
                DetectSourceSql,
                operationName: "InvoiceSourceSelector.Select");

            var selected = availability is { HasLegacyLedger: false, HasBiFact: true }
                ? InvoiceDataSource.Bi
                : InvoiceDataSource.Legacy;

            SelectionCache[cacheKey] = selected;
            return selected;
        }

        private sealed class InvoiceSourceAvailabilityRow
        {
            public bool HasLegacyLedger { get; set; }
            public bool HasBiFact { get; set; }
        }
    }
}

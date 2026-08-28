using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WebApp.HealthChecks
{
    /// <summary>
    /// Simple SQL health check that opens a connection and runs SELECT 1.
    /// </summary>
    public class SqlConnectionHealthCheck : IHealthCheck
    {
        private readonly string _connectionString;

        public SqlConnectionHealthCheck(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                await using var cmd = new SqlCommand("SELECT 1;", conn);
                await cmd.ExecuteScalarAsync(cancellationToken);

                return HealthCheckResult.Healthy("SQL connection OK");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("SQL connection failed", ex);
            }
        }
    }
}

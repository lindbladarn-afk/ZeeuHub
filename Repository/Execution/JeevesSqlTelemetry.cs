using System.Diagnostics;

namespace Repository.Execution;

// Defines dependency tracing and stable operational codes for Jeeves SQL access.
public static class JeevesSqlTelemetry
{
    public const string ActivitySourceName = "ZeeU.CustomerPortal.JeevesSql";
    public const string ConnectionFailedErrorCode = "JEEVES_CONNECTION_FAILED";
    public const string QueryFailedErrorCode = "JEEVES_QUERY_FAILED";
    public const string QueryTimeoutErrorCode = "JEEVES_QUERY_TIMEOUT";
    public const string SlowQueryErrorCode = "JEEVES_SLOW_QUERY";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}

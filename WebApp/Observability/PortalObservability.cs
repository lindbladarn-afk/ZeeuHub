using System.Diagnostics;

namespace WebApp.Observability;

// Defines shared telemetry names and request keys used across portal modules.
public static class PortalObservability
{
    public const string ActivitySourceName = "ZeeU.CustomerPortal";
    public const string CorrelationHeaderName = "X-Correlation-ID";
    public const string SupportHeaderName = "X-Support-ID";
    public const string CorrelationIdItemKey = "Observability.CorrelationId";
    public const string SupportIdItemKey = "Observability.SupportId";
    public const string CompanyIdItemKey = "Observability.CompanyId";
    public const string JeevesCompanyCodeItemKey = "Observability.JeevesCompanyCode";
    public const string UserIdItemKey = "Observability.UserId";
    public const string ModuleItemKey = "Observability.Module";
    public const string OperationItemKey = "Observability.Operation";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}

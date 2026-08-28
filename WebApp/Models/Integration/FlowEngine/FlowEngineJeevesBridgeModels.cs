namespace WebApp.Models.Integration;

public enum FlowEngineJeevesLookupStatus
{
    Found,
    NotFound,
    Error
}

public sealed class FlowEngineJeevesOrderCheckResult
{
    public FlowEngineJeevesLookupStatus Status { get; set; }
    public int JeevesOrderStatus { get; set; }
    public int? JeevesOrderNumber { get; set; }
    public string? StatusName { get; set; }
    public string? TrackingUrl { get; set; }
    public string? ErrorMessage { get; set; }
}

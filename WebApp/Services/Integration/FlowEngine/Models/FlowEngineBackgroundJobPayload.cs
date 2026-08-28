using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineBackgroundJobPayload
{
    public Guid CompanyId { get; set; }
    public Guid FlowEngineJobId { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int? JeevesActiveCompany { get; set; }
    public FlowEngineExecuteJobRequest Request { get; set; } = new();
}

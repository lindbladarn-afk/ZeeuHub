using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineJobStore
{
    FlowEngineJobSnapshot Create(Guid companyId, string? userId, string? userName, string[] arguments, FlowEngineExecuteJobRequest request);
    FlowEngineJobSnapshot MarkRunning(Guid companyId, Guid jobId, DateTimeOffset startedAtUtc);
    FlowEngineJobSnapshot Complete(Guid companyId, Guid jobId, FlowEngineJobResultPayload result);
    FlowEngineJobSnapshot Fail(Guid companyId, Guid jobId, FlowEngineJobResultPayload result, string errorMessage);
    FlowEngineJobSnapshot? Get(Guid companyId, Guid jobId);
    IReadOnlyList<FlowEngineJobSnapshot> ListRecent(Guid companyId, int take);
    FlowEngineHistoryPageResult ListPage(Guid companyId, int page, int pageSize, string? systemKey = null, FlowEngineHistoryFilterState? filters = null);
}

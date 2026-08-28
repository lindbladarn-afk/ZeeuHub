using Entities.Application;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineExecutionService
{
    Task<FlowEngineJobSnapshot> ExecuteAsync(
        UserSession sessionUser,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken = default);

    FlowEngineJobSnapshot? Get(Guid companyId, Guid jobId);
    IReadOnlyList<FlowEngineJobSnapshot> ListRecent(Guid companyId, int take = 10);
    FlowEngineHistoryPageResult ListPage(Guid companyId, int page = 1, int pageSize = 15, string? systemKey = null, FlowEngineHistoryFilterState? filters = null);
}

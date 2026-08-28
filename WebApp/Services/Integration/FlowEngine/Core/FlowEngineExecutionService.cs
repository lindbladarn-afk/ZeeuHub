using Entities.Application;
using Microsoft.AspNetCore.Routing;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.Models.BackgroundJobs;
using WebApp.Services.Application.BackgroundJobs;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineExecutionService : IFlowEngineExecutionService
{
    private readonly IFlowEngineCommandLineBuilder _commandLineBuilder;
    private readonly IJeevesRuntimeContextService _jeevesRuntimeContextService;
    private readonly IFlowEngineJobStore _jobStore;
    private readonly IBackgroundJobStore _backgroundJobStore;
    private readonly ISidebarRuntimeStatusService _sidebarRuntimeStatusService;
    private readonly LinkGenerator _linkGenerator;

    public FlowEngineExecutionService(
        IFlowEngineCommandLineBuilder commandLineBuilder,
        IJeevesRuntimeContextService jeevesRuntimeContextService,
        IFlowEngineJobStore jobStore,
        IBackgroundJobStore backgroundJobStore,
        ISidebarRuntimeStatusService sidebarRuntimeStatusService,
        LinkGenerator linkGenerator)
    {
        _commandLineBuilder = commandLineBuilder;
        _jeevesRuntimeContextService = jeevesRuntimeContextService;
        _jobStore = jobStore;
        _backgroundJobStore = backgroundJobStore;
        _sidebarRuntimeStatusService = sidebarRuntimeStatusService;
        _linkGenerator = linkGenerator;
    }

    public async Task<FlowEngineJobSnapshot> ExecuteAsync(
        UserSession sessionUser,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken = default)
    {
        if (sessionUser.CompanyId is not Guid companyId || companyId == Guid.Empty)
            throw new InvalidOperationException("Anvandarens bolag kunde inte faststallas for FlowEngine.");

        var runtimeContextResult = await _jeevesRuntimeContextService.ResolveAsync(sessionUser, cancellationToken);
        if (!runtimeContextResult.Success || runtimeContextResult.Value is null)
            throw new InvalidOperationException(runtimeContextResult.Error ?? "Jeeves runtime context kunde inte faststallas.");

        var runtimeContext = runtimeContextResult.Value;
        ApplyRuntimeDefaults(request, runtimeContext);
        var arguments = _commandLineBuilder.BuildArguments(request, runtimeContext).ToArray();

        var flowEngineJob = _jobStore.Create(
            companyId,
            sessionUser.UserId,
            BuildRequestedBy(sessionUser),
            arguments,
            request);

        var backgroundPayload = new FlowEngineBackgroundJobPayload
        {
            CompanyId = companyId,
            FlowEngineJobId = flowEngineJob.Id,
            UserId = sessionUser.UserId,
            Email = sessionUser.Email,
            FirstName = sessionUser.FirstName,
            LastName = sessionUser.LastName,
            JeevesActiveCompany = sessionUser.JeevesActiveCompany,
            Request = request
        };

        _backgroundJobStore.Enqueue(
            new BackgroundJobEnqueueRequest
            {
                CompanyId = companyId,
                CreatedByUserId = sessionUser.UserId,
                CreatedByEmail = sessionUser.Email,
                JobType = FlowEngineBackgroundJobConstants.ExecuteJobType,
                CorrelationKey = $"flowengine:{flowEngineJob.Id:N}",
                PayloadJson = FlowEngineQueuedJobProcessor.SerializePayload(backgroundPayload)
            },
            DateTime.UtcNow);

        _sidebarRuntimeStatusService.RecordEvent(
            companyId,
            FlowEngineRuntimeEventFactory.CreateQueued(companyId, flowEngineJob, _linkGenerator));

        return flowEngineJob;
    }

    public FlowEngineJobSnapshot? Get(Guid companyId, Guid jobId)
        => _jobStore.Get(companyId, jobId);

    public IReadOnlyList<FlowEngineJobSnapshot> ListRecent(Guid companyId, int take = 10)
        => _jobStore.ListRecent(companyId, take);

    public FlowEngineHistoryPageResult ListPage(Guid companyId, int page = 1, int pageSize = 15, string? systemKey = null, FlowEngineHistoryFilterState? filters = null)
        => _jobStore.ListPage(companyId, page, pageSize, systemKey, filters);

    private static void ApplyRuntimeDefaults(FlowEngineExecuteJobRequest request, JeevesRuntimeContext runtimeContext)
    {
        request.Params ??= new FlowEngineExecutionParams();

        if (!request.Params.JeevesCompanyCode.HasValue || request.Params.JeevesCompanyCode <= 0)
            request.Params.JeevesCompanyCode = runtimeContext.CompanyCode;

        if (request.Params.JeevesImportOrder is not null)
        {
            if (!request.Params.JeevesImportOrder.CompanyCode.HasValue || request.Params.JeevesImportOrder.CompanyCode <= 0)
                request.Params.JeevesImportOrder.CompanyCode = runtimeContext.CompanyCode;

            if (request.Params.JeevesImportOrder.OrderType.GetValueOrDefault() <= 0)
                request.Params.JeevesImportOrder.OrderType = 1;

            if (string.IsNullOrWhiteSpace(request.Params.JeevesImportOrder.ExternalOrderNumber))
                request.Params.JeevesImportOrder.ExternalOrderNumber = GenerateImportExternalOrderNumber();
        }
    }

    private static string BuildRequestedBy(UserSession sessionUser)
    {
        var fullName = $"{sessionUser.FirstName} {sessionUser.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName)
            ? sessionUser.Email ?? sessionUser.UserId
            : fullName;
    }

    private static string GenerateImportExternalOrderNumber()
    {
        var bounded = Math.Clamp(Random.Shared.NextDouble(), 0d, 0.999999999999d);
        var number = (int)Math.Floor(bounded * 1_000_000_000d);
        return $"IMP-{number.ToString().PadLeft(9, '0')}";
    }
}

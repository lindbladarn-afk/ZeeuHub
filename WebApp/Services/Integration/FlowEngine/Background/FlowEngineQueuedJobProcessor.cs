using System.Text;
using System.Text.Json;
using Entities.Application;
using Microsoft.AspNetCore.Routing;
using WebApp.Models.Integration;
using WebApp.Observability;
using WebApp.Services.Application;
using WebApp.Services.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineQueuedJobProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IFlowEngineCommandLineBuilder _commandLineBuilder;
    private readonly IJeevesRuntimeContextService _jeevesRuntimeContextService;
    private readonly IFlowEngineOperationDispatcher _operationDispatcher;
    private readonly IFlowEngineJobStore _jobStore;
    private readonly ISidebarRuntimeStatusService _sidebarRuntimeStatusService;
    private readonly LinkGenerator _linkGenerator;
    private readonly ILogger<FlowEngineQueuedJobProcessor> _logger;

    public FlowEngineQueuedJobProcessor(
        IFlowEngineCommandLineBuilder commandLineBuilder,
        IJeevesRuntimeContextService jeevesRuntimeContextService,
        IFlowEngineOperationDispatcher operationDispatcher,
        IFlowEngineJobStore jobStore,
        ISidebarRuntimeStatusService sidebarRuntimeStatusService,
        LinkGenerator linkGenerator,
        ILogger<FlowEngineQueuedJobProcessor> logger)
    {
        _commandLineBuilder = commandLineBuilder;
        _jeevesRuntimeContextService = jeevesRuntimeContextService;
        _operationDispatcher = operationDispatcher;
        _jobStore = jobStore;
        _sidebarRuntimeStatusService = sidebarRuntimeStatusService;
        _linkGenerator = linkGenerator;
        _logger = logger;
    }

    public async Task<FlowEngineJobSnapshot> ProcessAsync(FlowEngineBackgroundJobPayload payload, CancellationToken cancellationToken)
    {
        if (payload.CompanyId == Guid.Empty)
            throw new InvalidOperationException("FlowEngine queue payload saknar company id.");

        var sessionUser = BuildSessionUser(payload);
        var runtimeContextResult = await _jeevesRuntimeContextService.ResolveAsync(sessionUser, cancellationToken);
        if (!runtimeContextResult.Success || runtimeContextResult.Value is null)
            throw new InvalidOperationException(runtimeContextResult.Error ?? "Jeeves runtime context kunde inte faststallas.");

        var runtimeContext = runtimeContextResult.Value;
        ApplyRuntimeDefaults(payload.Request, runtimeContext);
        var arguments = _commandLineBuilder.BuildArguments(payload.Request, runtimeContext).ToArray();

        var startedAtUtc = DateTimeOffset.UtcNow;
        var runningJob = _jobStore.MarkRunning(payload.CompanyId, payload.FlowEngineJobId, startedAtUtc);
        _sidebarRuntimeStatusService.RecordEvent(
            payload.CompanyId,
            FlowEngineRuntimeEventFactory.CreateRunning(payload.CompanyId, runningJob, _linkGenerator));

        try
        {
            var execution = await _operationDispatcher.DispatchAsync(runtimeContext, payload.Request, cancellationToken);
            var finishedAtUtc = DateTimeOffset.UtcNow;
            var result = new FlowEngineJobResultPayload
            {
                CommandLine = string.Join(' ', arguments),
                ExitCode = 0,
                Succeeded = true,
                StandardOutput = BuildStandardOutput(execution),
                StandardError = string.Empty,
                StartedAtUtc = startedAtUtc,
                FinishedAtUtc = finishedAtUtc
            };

            var completedJob = _jobStore.Complete(payload.CompanyId, payload.FlowEngineJobId, result);
            _sidebarRuntimeStatusService.RecordEvent(
                payload.CompanyId,
                FlowEngineRuntimeEventFactory.CreateCompleted(payload.CompanyId, completedJob, _linkGenerator));
            return completedJob;
        }
        catch (Exception ex)
        {
            var finishedAtUtc = DateTimeOffset.UtcNow;
            var diagnostic = IntegrationLogSanitizer.Diagnostic(ex.Message);
            _logger.LogError(
                ex,
                "FlowEngine operation failed. {ErrorCode} {CompanyId} {FlowEngineJobId} {Operation} {DurationMs}",
                PortalErrorCodes.FlowEngineExecutionFailed,
                payload.CompanyId,
                payload.FlowEngineJobId,
                payload.Request.Operation,
                (long)(finishedAtUtc - startedAtUtc).TotalMilliseconds);
            var result = new FlowEngineJobResultPayload
            {
                CommandLine = string.Join(' ', arguments),
                ExitCode = 1,
                Succeeded = false,
                StandardOutput = string.Empty,
                StandardError = diagnostic,
                StartedAtUtc = startedAtUtc,
                FinishedAtUtc = finishedAtUtc
            };

            var failedJob = _jobStore.Fail(payload.CompanyId, payload.FlowEngineJobId, result, diagnostic);
            _sidebarRuntimeStatusService.RecordEvent(
                payload.CompanyId,
                FlowEngineRuntimeEventFactory.CreateFailed(payload.CompanyId, failedJob, _linkGenerator));
            return failedJob;
        }
    }

    public static string SerializePayload(FlowEngineBackgroundJobPayload payload)
        => JsonSerializer.Serialize(payload, JsonOptions);

    public static FlowEngineBackgroundJobPayload DeserializePayload(string payloadJson)
        => JsonSerializer.Deserialize<FlowEngineBackgroundJobPayload>(payloadJson, JsonOptions)
           ?? throw new InvalidOperationException("FlowEngine background job payload could not be deserialized.");

    public static void ApplyRuntimeDefaults(FlowEngineExecuteJobRequest request, JeevesRuntimeContext runtimeContext)
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

    private static UserSession BuildSessionUser(FlowEngineBackgroundJobPayload payload)
        => new()
        {
            UserId = payload.UserId ?? string.Empty,
            Email = payload.Email,
            FirstName = payload.FirstName,
            LastName = payload.LastName,
            CompanyId = payload.CompanyId,
            JeevesActiveCompany = payload.JeevesActiveCompany
        };

    private static string BuildStandardOutput(FlowEngineOperationExecutionData execution)
    {
        var builder = new StringBuilder();

        foreach (var line in execution.SummaryLines.Where(line => !string.IsNullOrWhiteSpace(line)))
            builder.AppendLine(line);

        if (builder.Length > 0)
            builder.AppendLine();

        builder.Append(execution.JsonOutput);
        return builder.ToString().Trim();
    }

    private static string GenerateImportExternalOrderNumber()
    {
        var bounded = Math.Clamp(Random.Shared.NextDouble(), 0d, 0.999999999999d);
        var number = (int)Math.Floor(bounded * 1_000_000_000d);
        return $"IMP-{number.ToString().PadLeft(9, '0')}";
    }
}

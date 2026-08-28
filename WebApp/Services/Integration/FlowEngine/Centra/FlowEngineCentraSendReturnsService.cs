using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.Services.Integration;
using WebApp.Services.Integration.FlowEngine.CentraSendReturnsContracts;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraSendReturnsService : IFlowEngineCentraSendReturnsService
{
    private const int CentraOriginJeevesCompanyCode = 1;
    private const int DefaultPageSize = 50;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly IFlowEngineCentraConnectionService _centraConnectionService;
    private readonly IFlowEngineCentraGraphQlClient _centraGraphQlClient;
    private readonly IFlowEngineCentraQueryCatalog _centraQueryCatalog;
    private readonly IFlowEngineCentraJeevesBridgeService _jeevesBridgeService;
    private readonly IFlowEngineCentraSendReturnsResultFactory _resultFactory;
    private readonly ILogger<FlowEngineCentraSendReturnsService> _logger;

    public FlowEngineCentraSendReturnsService(
        IFlowEngineCentraConnectionService centraConnectionService,
        IFlowEngineCentraGraphQlClient centraGraphQlClient,
        IFlowEngineCentraQueryCatalog centraQueryCatalog,
        IFlowEngineCentraJeevesBridgeService jeevesBridgeService,
        IFlowEngineCentraSendReturnsResultFactory resultFactory,
        ILogger<FlowEngineCentraSendReturnsService> logger)
    {
        _centraConnectionService = centraConnectionService;
        _centraGraphQlClient = centraGraphQlClient;
        _centraQueryCatalog = centraQueryCatalog;
        _jeevesBridgeService = jeevesBridgeService;
        _resultFactory = resultFactory;
        _logger = logger;
    }

    public async Task<FlowEngineOperationExecutionData> ExecuteAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Operation == FlowEngineOperationType.SendReturn)
            return await ExecuteSingleReturnAsync(runtimeContext, request, cancellationToken);

        var targetDateUtc = FlowEngineCentraCommonHelper.ResolveTargetDateUtc(request.Params.DateUtc, "send returns");
        var dayStartUtc = DateTime.SpecifyKind(targetDateUtc.Date, DateTimeKind.Utc);
        var dayEndUtc = dayStartUtc.AddDays(1);
        var limit = request.Params.UseLimit ? request.Params.Limit : null;
        var dryRun = request.Flags.DryRun;

        var centraConfig = _centraConnectionService.ResolveConfig(runtimeContext.CompanyId, "send returns", request.Flags.TestMode);
        var jeevesConfig = _jeevesBridgeService.ResolveConfig(runtimeContext.CompanyId, "send returns");
        var stopwatch = Stopwatch.StartNew();

        var returns = await FetchReturnsByDateAsync(centraConfig, dayStartUtc, dayEndUtc, cancellationToken);
        var ordered = returns
            .OrderBy(item => item.CreatedAt ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(item => item.Id)
            .ToList();

        if (limit.HasValue && limit.Value > 0)
            ordered = ordered.Take(limit.Value).ToList();

        var results = new List<FlowEngineSendReturnsRow>(ordered.Count);
        foreach (var returnData in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ProcessReturnAsync(runtimeContext.CompanyId, jeevesConfig, returnData, dryRun, cancellationToken);
            results.Add(result);
        }

        if (!dryRun)
        {
            await FlowEngineCentraSendReturnsRetryHelper.RetryDeadlockFailuresSequentiallyAsync(
                runtimeContext.CompanyId,
                jeevesConfig,
                ordered,
                results,
                _jeevesBridgeService,
                JsonOptions,
                cancellationToken);
        }

        stopwatch.Stop();

        var counts = new FlowEngineSendReturnsCounts
        {
            CentraTotal = ordered.Count
        };
        var nonCleanRows = new List<FlowEngineSendReturnsRow>();
        foreach (var result in results)
        {
            switch (result.Status)
            {
                case "mapped":
                case "sent":
                case "failed_api":
                    counts.Mapped++;
                    break;
                case "skipped_ineligible":
                    counts.SkippedIneligible++;
                    break;
                case "failed_validation":
                    counts.FailedValidation++;
                    break;
                case "already_exists":
                    counts.AlreadyExists++;
                    break;
                case "failed_mapping":
                    counts.FailedMapping++;
                    break;
            }

            if (result.Status == "failed_api")
                counts.FailedApi++;

            if (!string.Equals(result.Status, "mapped", StringComparison.Ordinal) &&
                !string.Equals(result.Status, "sent", StringComparison.Ordinal))
            {
                nonCleanRows.Add(result);
            }
        }

        return _resultFactory.CreateBulkResult(
            runtimeContext,
            dayStartUtc.ToString("yyyy-MM-dd"),
            limit,
            dryRun,
            counts,
            Math.Round(stopwatch.Elapsed.TotalSeconds, 2),
            nonCleanRows);
    }

    private async Task<FlowEngineOperationExecutionData> ExecuteSingleReturnAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken)
    {
        var returnIdRaw = string.IsNullOrWhiteSpace(request.Params.ReturnId)
            ? throw new InvalidOperationException("Return ID maste anges for Centra send return.")
            : request.Params.ReturnId.Trim();
        var dryRun = request.Flags.DryRun;

        if (!int.TryParse(returnIdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var returnId) || returnId <= 0)
            throw new InvalidOperationException("Return ID maste vara ett positivt heltal for Centra send return.");

        var centraConfig = _centraConnectionService.ResolveConfig(runtimeContext.CompanyId, "send returns", request.Flags.TestMode);
        var jeevesConfig = _jeevesBridgeService.ResolveConfig(runtimeContext.CompanyId, "send returns");
        var returnData = await FetchReturnByIdAsync(centraConfig, returnId, cancellationToken);
        var result = await ProcessReturnAsync(runtimeContext.CompanyId, jeevesConfig, returnData, dryRun, cancellationToken);

        return _resultFactory.CreateSingleResult(runtimeContext, returnId, dryRun, result);
    }

    private async Task<FlowEngineSendReturnsRow> ProcessReturnAsync(
        Guid companyId,
        IntegrationSourceConfig jeevesConfig,
        CentraRawReturn returnData,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var validation = FlowEngineCentraSendReturnsValidator.Validate(returnData);
        if (validation.ValidationFailures.Count > 0)
        {
            return BuildRow(returnData, "failed_validation", "Validation failed", validation.ValidationFailures, validation.EligibilityFailures);
        }

        if (validation.EligibilityFailures.Count > 0)
        {
            return BuildRow(returnData, "skipped_ineligible", null, validation.ValidationFailures, validation.EligibilityFailures);
        }

        try
        {
            var extOrderNr = $"C{returnData.Id}";
            if (!dryRun)
            {
                var exists = await _jeevesBridgeService.OrderExistsAsync(companyId, jeevesConfig, CentraOriginJeevesCompanyCode, extOrderNr, cancellationToken);
                if (exists)
                    return BuildRow(returnData, "already_exists", null, validation.ValidationFailures, validation.EligibilityFailures);
            }

            var payload = FlowEngineCentraReturnJeevesMapper.Map(returnData);
            if (dryRun)
                return BuildRow(returnData, "mapped", null, validation.ValidationFailures, validation.EligibilityFailures);

            await _jeevesBridgeService.CreateOrderAsync(
                companyId,
                jeevesConfig,
                JsonSerializer.Serialize(payload, JsonOptions),
                "send returns",
                cancellationToken);
            return BuildRow(returnData, "sent", null, validation.ValidationFailures, validation.EligibilityFailures);
        }
        catch (FlowEngineCentraJeevesDuplicateOrderException)
        {
            return BuildRow(returnData, "already_exists", null, validation.ValidationFailures, validation.EligibilityFailures);
        }
        catch (ReturnMappingException ex)
        {
            _logger.LogWarning(ex, "FlowEngine Centra send returns mapping failed for return {ReturnId}.", returnData.Id);
            return BuildRow(returnData, "failed_mapping", ex.Message, validation.ValidationFailures, validation.EligibilityFailures);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FlowEngine Centra send returns API failed for return {ReturnId}.", returnData.Id);
            return BuildRow(returnData, "failed_api", ex.Message, validation.ValidationFailures, validation.EligibilityFailures);
        }
    }
    private FlowEngineSendReturnsRow BuildRow(
        CentraRawReturn returnData,
        string status,
        string? errorMessage,
        List<FlowEngineSendReturnsRuleFailure> validationFailures,
        List<FlowEngineSendReturnsRuleFailure> eligibilityFailures)
    {
        return FlowEngineCentraSendReturnsRowBuilder.Create(returnData, status, errorMessage, validationFailures, eligibilityFailures);
    }
    private async Task<List<CentraRawReturn>> FetchReturnsByDateAsync(
        IntegrationSourceConfig centraConfig,
        DateTime dayStartUtc,
        DateTime dayEndUtc,
        CancellationToken cancellationToken)
    {
        var results = new List<CentraRawReturn>();
        var page = 1;

        while (true)
        {
            var payload = new
            {
                query = _centraQueryCatalog.GetSendReturnsByDateQuery(),
                variables = new
                {
                    from = dayStartUtc.ToString("O", CultureInfo.InvariantCulture),
                    to = dayEndUtc.ToString("O", CultureInfo.InvariantCulture),
                    limit = DefaultPageSize,
                    page
                },
                operationName = "ReturnsByDatePaginated"
            };

            var body = await _centraGraphQlClient.PostAsync(centraConfig, payload, cancellationToken);

            if (FlowEngineCentraCommonHelper.TryGetGraphQlErrorMessage(body, out var graphQlError))
                throw new InvalidOperationException($"Centra GraphQL: {graphQlError}");

            var parsed = JsonSerializer.Deserialize<CentraReturnsResponse>(body, JsonOptions);
            var pageReturns = parsed?.Data?.Returns ?? new List<CentraRawReturn>();
            if (pageReturns.Count == 0)
                break;

            results.AddRange(pageReturns);
            if (pageReturns.Count < DefaultPageSize)
                break;

            page++;
        }

        return results;
    }

    private async Task<CentraRawReturn> FetchReturnByIdAsync(
        IntegrationSourceConfig centraConfig,
        int returnId,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            query = _centraQueryCatalog.GetSendReturnByIdQuery(),
            variables = new { id = new[] { returnId } },
            operationName = "Return"
        };

        var body = await _centraGraphQlClient.PostAsync(centraConfig, payload, cancellationToken);

        if (FlowEngineCentraCommonHelper.TryGetGraphQlErrorMessage(body, out var graphQlError))
            throw new InvalidOperationException($"Centra GraphQL: {graphQlError}");

        var parsed = JsonSerializer.Deserialize<CentraReturnsResponse>(body, JsonOptions);
        var returnData = parsed?.Data?.Returns?.FirstOrDefault();
        return returnData ?? throw new InvalidOperationException($"Centra return hittades inte for {returnId}.");
    }
}

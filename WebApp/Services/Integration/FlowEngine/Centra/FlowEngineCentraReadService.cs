using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraReadService : IFlowEngineCentraReadService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly IFlowEngineCentraConnectionService _centraConnectionService;
    private readonly IFlowEngineCentraGraphQlClient _centraGraphQlClient;
    private readonly IFlowEngineCentraQueryCatalog _centraQueryCatalog;
    private readonly IFlowEngineCentraReadResultFactory _resultFactory;
    private readonly FlowEngineCentraReadSelectionService _selectionService;
    private readonly FlowEngineCentraPagedReadCollector _pagedReadCollector;
    private readonly ILogger<FlowEngineCentraReadService> _logger;

    public FlowEngineCentraReadService(
        IFlowEngineCentraConnectionService centraConnectionService,
        IFlowEngineCentraGraphQlClient centraGraphQlClient,
        IFlowEngineCentraQueryCatalog centraQueryCatalog,
        IFlowEngineCentraReadResultFactory resultFactory,
        FlowEngineCentraReadSelectionService selectionService,
        FlowEngineCentraPagedReadCollector pagedReadCollector,
        ILogger<FlowEngineCentraReadService> logger)
    {
        _centraConnectionService = centraConnectionService;
        _centraGraphQlClient = centraGraphQlClient;
        _centraQueryCatalog = centraQueryCatalog;
        _resultFactory = resultFactory;
        _selectionService = selectionService;
        _pagedReadCollector = pagedReadCollector;
        _logger = logger;
    }

    public async Task<FlowEngineOperationExecutionData> ExecuteAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken = default)
    {
        var centraConfig = _centraConnectionService.ResolveConfig(runtimeContext.CompanyId, "fetch", request.Flags.TestMode);

        return request.Operation switch
        {
            FlowEngineOperationType.CentraFetchOrder => await ExecuteFetchOrderAsync(runtimeContext, centraConfig, request, cancellationToken),
            FlowEngineOperationType.CentraFetchOrders => await ExecuteFetchOrdersAsync(runtimeContext, centraConfig, request, cancellationToken),
            FlowEngineOperationType.CentraFetchReturn => await ExecuteFetchReturnAsync(runtimeContext, centraConfig, request, cancellationToken),
            FlowEngineOperationType.CentraFetchReturns => await ExecuteFetchReturnsAsync(runtimeContext, centraConfig, request, cancellationToken),
            _ => throw new InvalidOperationException($"Operationen {request.Operation} stods inte av Centra read-tjansten.")
        };
    }

    private async Task<FlowEngineOperationExecutionData> ExecuteFetchOrderAsync(
        JeevesRuntimeContext runtimeContext,
        IntegrationSourceConfig centraConfig,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken)
    {
        var orderId = _selectionService.NormalizeRequiredValue(request.Params.OrderId, "Centra order-id");
        var body = await SendGraphQlAsync(
            centraConfig,
            new
            {
                query = _centraQueryCatalog.GetFetchOrderQuery(),
                variables = new { id = orderId },
                operationName = "Order"
            },
            cancellationToken);

        return _resultFactory.CreateFetchOrderResult(runtimeContext, orderId, body);
    }

    private async Task<FlowEngineOperationExecutionData> ExecuteFetchOrdersAsync(
        JeevesRuntimeContext runtimeContext,
        IntegrationSourceConfig centraConfig,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken)
    {
        var selection = _selectionService.ResolveDateSelection(request);
        var dates = _selectionService.EnumerateDates(selection.SinceUtc, selection.UntilUtc);
        if (selection.SelectionKind == "range" && dates.Count > 7 && !request.Flags.ForceRange)
            throw new InvalidOperationException($"Centra range ar {dates.Count} dagar. Anvand Force range for att overskrida 7 dagar.");

        var days = new List<object>(dates.Count);
        var totalOrders = 0;
        var totalGraphQlErrors = 0;
        var failedDays = 0;

        foreach (var date in dates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dayLabel = _selectionService.FormatDateUtc(date);

            try
            {
                var result = await _pagedReadCollector.CollectAsync(
                    centraConfig,
                    _centraQueryCatalog.GetFetchOrdersByDateQuery(),
                    "OrdersByDatePaginated",
                    "orders",
                    date,
                    cancellationToken);

                totalOrders += result.Items.Count;
                totalGraphQlErrors += result.Errors.Count;
                days.Add(new
                {
                    date = dayLabel,
                    data = new
                    {
                        orders = result.Items
                    },
                    errors = result.Errors.Count > 0 ? result.Errors : null
                });
            }
            catch (Exception ex)
            {
                failedDays++;
                _logger.LogWarning(ex, "FlowEngine Centra fetch-orders failed for date {Date}.", dayLabel);
                days.Add(new
                {
                    date = dayLabel,
                    errorMessage = ex.Message
                });
            }
        }

        return _resultFactory.CreateFetchOrdersResult(
            runtimeContext,
            selection.SelectionKind,
            dates,
            selection.SinceUtc,
            selection.UntilUtc,
            failedDays,
            totalOrders,
            totalGraphQlErrors,
            days);
    }

    private async Task<FlowEngineOperationExecutionData> ExecuteFetchReturnAsync(
        JeevesRuntimeContext runtimeContext,
        IntegrationSourceConfig centraConfig,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken)
    {
        var returnIdRaw = _selectionService.NormalizeRequiredValue(request.Params.ReturnId, "Centra return-id");
        if (!int.TryParse(returnIdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var returnId) || returnId <= 0)
            throw new InvalidOperationException("Centra return-id maste vara ett positivt heltal.");

        var body = await SendGraphQlAsync(
            centraConfig,
            new
            {
                query = _centraQueryCatalog.GetFetchReturnQuery(),
                variables = new { id = new[] { returnId } },
                operationName = "Return"
            },
            cancellationToken);

        return _resultFactory.CreateFetchReturnResult(runtimeContext, returnId, body);
    }

    private async Task<FlowEngineOperationExecutionData> ExecuteFetchReturnsAsync(
        JeevesRuntimeContext runtimeContext,
        IntegrationSourceConfig centraConfig,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken)
    {
        var selection = _selectionService.ResolveDateSelection(request);
        var dates = _selectionService.EnumerateDates(selection.SinceUtc, selection.UntilUtc);
        if (selection.SelectionKind == "range" && dates.Count > 7 && !request.Flags.ForceRange)
            throw new InvalidOperationException($"Centra range ar {dates.Count} dagar. Anvand Force range for att overskrida 7 dagar.");

        var days = new List<object>(dates.Count);
        var totalReturns = 0;
        var totalGraphQlErrors = 0;
        var failedDays = 0;

        foreach (var date in dates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dayLabel = _selectionService.FormatDateUtc(date);

            try
            {
                var result = await _pagedReadCollector.CollectAsync(
                    centraConfig,
                    _centraQueryCatalog.GetFetchReturnsByDateQuery(),
                    "ReturnsByDatePaginated",
                    "returns",
                    date,
                    cancellationToken);

                totalReturns += result.Items.Count;
                totalGraphQlErrors += result.Errors.Count;
                days.Add(new
                {
                    date = dayLabel,
                    data = new
                    {
                        returns = result.Items
                    },
                    errors = result.Errors.Count > 0 ? result.Errors : null
                });
            }
            catch (Exception ex)
            {
                failedDays++;
                _logger.LogWarning(ex, "FlowEngine Centra fetch-returns failed for date {Date}.", dayLabel);
                days.Add(new
                {
                    date = dayLabel,
                    errorMessage = ex.Message
                });
            }
        }

        return _resultFactory.CreateFetchReturnsResult(
            runtimeContext,
            selection.SelectionKind,
            dates,
            selection.SinceUtc,
            selection.UntilUtc,
            failedDays,
            totalReturns,
            totalGraphQlErrors,
            days);
    }

    private async Task<string> SendGraphQlAsync(
        IntegrationSourceConfig centraConfig,
        object payload,
        CancellationToken cancellationToken)
    {
        return await _centraGraphQlClient.PostAsync(centraConfig, payload, cancellationToken);
    }
}

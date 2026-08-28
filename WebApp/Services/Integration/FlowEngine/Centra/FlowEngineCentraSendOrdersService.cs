using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.Services.Integration;
using WebApp.Services.Integration.FlowEngine.CentraSendOrdersContracts;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraSendOrdersService : IFlowEngineCentraSendOrdersService
{
    private const int CentraOriginJeevesCompanyCode = 1;
    private const int DefaultPageSize = 50;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions GraphQlJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly IFlowEngineCentraConnectionService _centraConnectionService;
    private readonly IFlowEngineCentraGraphQlClient _centraGraphQlClient;
    private readonly IFlowEngineCentraQueryCatalog _centraQueryCatalog;
    private readonly IFlowEngineCentraJeevesBridgeService _jeevesBridgeService;
    private readonly IFlowEngineCentraSendOrdersResultFactory _resultFactory;
    private readonly ILogger<FlowEngineCentraSendOrdersService> _logger;

    public FlowEngineCentraSendOrdersService(
        IFlowEngineCentraConnectionService centraConnectionService,
        IFlowEngineCentraGraphQlClient centraGraphQlClient,
        IFlowEngineCentraQueryCatalog centraQueryCatalog,
        IFlowEngineCentraJeevesBridgeService jeevesBridgeService,
        IFlowEngineCentraSendOrdersResultFactory resultFactory,
        ILogger<FlowEngineCentraSendOrdersService> logger)
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
        if (request.Operation == FlowEngineOperationType.SendOrder)
            return await ExecuteSingleOrderAsync(runtimeContext, request, cancellationToken);

        var targetDateUtc = FlowEngineCentraCommonHelper.ResolveTargetDateUtc(request.Params.DateUtc, "send orders");
        var dayStartUtc = DateTime.SpecifyKind(targetDateUtc.Date, DateTimeKind.Utc);
        var dayEndUtc = dayStartUtc.AddDays(1);
        var limit = request.Params.UseLimit ? request.Params.Limit : null;
        var dryRun = request.Flags.DryRun;
        var skipJeevesCheck = request.Flags.SkipJeevesCheck;

        var centraConfig = _centraConnectionService.ResolveConfig(runtimeContext.CompanyId, "send orders", request.Flags.TestMode);
        var jeevesConfig = _jeevesBridgeService.ResolveConfig(runtimeContext.CompanyId, "send orders");
        var stopwatch = Stopwatch.StartNew();

        var orders = await FetchOrdersByDateAsync(centraConfig, dayStartUtc, dayEndUtc, cancellationToken);
        var ordered = orders
            .OrderBy(order => order.CreatedAt ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(order => order.Id, StringComparer.Ordinal)
            .ToList();

        if (limit.HasValue && limit.Value > 0)
            ordered = ordered.Take(limit.Value).ToList();

        var counts = new FlowEngineSendOrdersCounts();
        var nonCleanRows = new List<FlowEngineSendOrdersRow>();

        foreach (var order in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await ProcessOrderAsync(runtimeContext.CompanyId, jeevesConfig, order, dryRun, skipJeevesCheck, cancellationToken);
            switch (result.Status)
            {
                case "mapped":
                case "sent":
                    counts.Mapped++;
                    break;
                case "skipped_existing":
                    counts.SkippedExisting++;
                    break;
                case "skipped_ineligible":
                    counts.SkippedIneligible++;
                    break;
                case "manual_review_required":
                    counts.ManualReviewRequired++;
                    break;
                case "failed":
                    counts.Failed++;
                    break;
            }

            if (!string.Equals(result.Status, "mapped", StringComparison.Ordinal) &&
                !string.Equals(result.Status, "sent", StringComparison.Ordinal))
            {
                nonCleanRows.Add(result);
            }
        }

        stopwatch.Stop();
        counts.CentraTotal = ordered.Count;

        return _resultFactory.CreateBulkResult(
            runtimeContext,
            dayStartUtc.ToString("yyyy-MM-dd"),
            limit,
            dryRun,
            skipJeevesCheck,
            counts,
            Math.Round(stopwatch.Elapsed.TotalSeconds, 2),
            nonCleanRows);
    }

    private async Task<FlowEngineOperationExecutionData> ExecuteSingleOrderAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken)
    {
        var orderId = string.IsNullOrWhiteSpace(request.Params.OrderId)
            ? throw new InvalidOperationException("Order ID maste anges for Centra send order.")
            : request.Params.OrderId.Trim();
        var dryRun = request.Flags.DryRun;
        var skipJeevesCheck = request.Flags.SkipJeevesCheck;

        var centraConfig = _centraConnectionService.ResolveConfig(runtimeContext.CompanyId, "send orders", request.Flags.TestMode);
        var jeevesConfig = _jeevesBridgeService.ResolveConfig(runtimeContext.CompanyId, "send orders");
        var order = await FetchOrderByIdAsync(centraConfig, orderId, cancellationToken);
        var result = await ProcessOrderAsync(runtimeContext.CompanyId, jeevesConfig, order, dryRun, skipJeevesCheck, cancellationToken);

        return _resultFactory.CreateSingleResult(runtimeContext, orderId, dryRun, skipJeevesCheck, result);
    }

    private async Task<FlowEngineSendOrdersRow> ProcessOrderAsync(
        Guid companyId,
        IntegrationSourceConfig jeevesConfig,
        CentraRawOrder order,
        bool dryRun,
        bool skipJeevesCheck,
        CancellationToken cancellationToken)
    {
        var validation = FlowEngineCentraSendOrdersValidator.Validate(order);
        if (validation.ValidationFailures.Count > 0)
        {
            return BuildRow(
                order,
                "failed",
                "Validation failed",
                null,
                validation.ValidationFailures,
                validation.EligibilityFailures);
        }

        if (validation.EligibilityFailures.Count > 0)
        {
            return BuildRow(
                order,
                "skipped_ineligible",
                null,
                null,
                validation.ValidationFailures,
                validation.EligibilityFailures);
        }

        try
        {
            var payload = FlowEngineCentraOrderJeevesMapper.Map(order);
            var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
            if (dryRun)
            {
                return BuildRow(
                    order,
                    "mapped",
                    null,
                    payloadJson,
                    validation.ValidationFailures,
                    validation.EligibilityFailures);
            }

            if (!skipJeevesCheck)
            {
                var exists = await _jeevesBridgeService.OrderExistsAsync(companyId, jeevesConfig, CentraOriginJeevesCompanyCode, order.Id, cancellationToken);
                if (exists)
                {
                    return BuildRow(
                        order,
                        "skipped_existing",
                        null,
                        payloadJson,
                        validation.ValidationFailures,
                        validation.EligibilityFailures);
                }
            }

            try
            {
                await _jeevesBridgeService.CreateOrderAsync(
                    companyId,
                    jeevesConfig,
                    payloadJson,
                    "send orders",
                    cancellationToken);
                return BuildRow(
                    order,
                    "sent",
                    null,
                    payloadJson,
                    validation.ValidationFailures,
                    validation.EligibilityFailures);
            }
            catch (FlowEngineCentraJeevesDuplicateOrderException)
            {
                return BuildRow(
                    order,
                    "skipped_existing",
                    null,
                    payloadJson,
                    validation.ValidationFailures,
                    validation.EligibilityFailures);
            }
            catch (FlowEngineCentraJeevesManualReviewException ex)
            {
                return BuildRow(
                    order,
                    "manual_review_required",
                    ex.Message,
                    payloadJson,
                    validation.ValidationFailures,
                    validation.EligibilityFailures);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FlowEngine Centra send orders failed for order {OrderId}.", order.Id);
            return BuildRow(
                order,
                "failed",
                ex.Message,
                null,
                validation.ValidationFailures,
                validation.EligibilityFailures);
        }
    }
    private FlowEngineSendOrdersRow BuildRow(
        CentraRawOrder order,
        string status,
        string? errorMessage,
        string? payloadJson,
        List<FlowEngineSendOrdersRuleFailure> validationFailures,
        List<FlowEngineSendOrdersRuleFailure> eligibilityFailures)
    {
        return FlowEngineCentraSendOrdersRowBuilder.Create(order, status, errorMessage, payloadJson, validationFailures, eligibilityFailures);
    }

    private async Task<List<CentraRawOrder>> FetchOrdersByDateAsync(
        IntegrationSourceConfig centraConfig,
        DateTime dayStartUtc,
        DateTime dayEndUtc,
        CancellationToken cancellationToken)
    {
        var orders = new List<CentraRawOrder>();
        var page = 1;

        while (true)
        {
            var payload = new
            {
                query = _centraQueryCatalog.GetSendOrdersByDateQuery(),
                variables = new
                {
                    from = dayStartUtc.ToString("O", CultureInfo.InvariantCulture),
                    to = dayEndUtc.ToString("O", CultureInfo.InvariantCulture),
                    limit = DefaultPageSize,
                    page
                },
                operationName = "OrdersByDatePaginatedFull"
            };

            var body = await _centraGraphQlClient.PostAsync(centraConfig, payload, cancellationToken);

            if (FlowEngineCentraCommonHelper.TryGetGraphQlErrorMessage(body, out var graphqlError))
                throw new InvalidOperationException($"Centra GraphQL: {graphqlError}");

            var parsed = JsonSerializer.Deserialize<CentraOrdersResponse>(body, GraphQlJsonOptions);
            var pageOrders = parsed?.Data?.Orders ?? new List<CentraRawOrder>();
            if (pageOrders.Count == 0)
                break;

            orders.AddRange(pageOrders);
            if (pageOrders.Count < DefaultPageSize)
                break;

            page++;
        }

        return orders;
    }

    private async Task<CentraRawOrder> FetchOrderByIdAsync(
        IntegrationSourceConfig centraConfig,
        string orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = new
            {
                query = _centraQueryCatalog.GetSendOrderByIdQuery(),
                variables = new { id = orderId },
                operationName = "Order"
            };

            var body = await _centraGraphQlClient.PostAsync(centraConfig, payload, cancellationToken);

            if (FlowEngineCentraCommonHelper.TryGetGraphQlErrorMessage(body, out var graphqlError))
                throw new InvalidOperationException($"Centra GraphQL: {graphqlError}");

            var parsed = JsonSerializer.Deserialize<CentraOrderByIdResponse>(body, GraphQlJsonOptions);
            if (parsed?.Data?.Order is not null)
                return parsed.Data.Order;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("404", StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "Centra order(id) lookup returned 404 for order {OrderId}; retrying via orders-lookup by id.",
                orderId);
        }

        var fallbackPayload = new
        {
            query = _centraQueryCatalog.GetSendOrderByLookupQuery(),
            variables = new { id = new[] { orderId }, limit = 1 },
            operationName = "OrdersByIdLookup"
        };

        var fallbackBody = await _centraGraphQlClient.PostAsync(centraConfig, fallbackPayload, cancellationToken);

        if (FlowEngineCentraCommonHelper.TryGetGraphQlErrorMessage(fallbackBody, out var fallbackGraphQlError))
            throw new InvalidOperationException($"Centra GraphQL: {fallbackGraphQlError}");

        var fallbackParsed = JsonSerializer.Deserialize<CentraOrdersResponse>(fallbackBody, GraphQlJsonOptions);
        var order = fallbackParsed?.Data?.Orders?.FirstOrDefault();
        return order ?? throw new InvalidOperationException(
            $"Centra order hittades inte for {orderId}. Ange Centra order-id (ex. 1234567) eller ett id som finns i Centra.");
    }
}

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineShopifyReadService : IFlowEngineShopifyReadService
{
    private const int DefaultPageSize = 50;
    private const string DefaultShopifyApiVersion = "2025-01";
    private const string ShopifyJeevesSyncedTag = "SentToJeeves";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly IFlowEngineShopifyConnectionService _shopifyConnectionService;
    private readonly IFlowEngineShopifyGraphQlClient _shopifyGraphQlClient;
    private readonly IFlowEngineShopifyScopeProbeService _shopifyScopeProbeService;
    private readonly IFlowEngineShopifySelectionService _shopifySelectionService;
    private readonly IFlowEngineShopifyReadResultFactory _shopifyReadResultFactory;
    private readonly IFlowEngineShopifyOrderValidator _shopifyOrderValidator;
    private readonly IFlowEngineShopifyOrderMapper _shopifyOrderMapper;
    private readonly IFlowEngineJeevesBridgeService _jeevesBridgeService;
    private readonly IFlowEngineShopifyQueryCatalog _shopifyQueryCatalog;

    public FlowEngineShopifyReadService(
        IFlowEngineShopifyConnectionService shopifyConnectionService,
        IFlowEngineShopifyGraphQlClient shopifyGraphQlClient,
        IFlowEngineShopifyScopeProbeService shopifyScopeProbeService,
        IFlowEngineShopifySelectionService shopifySelectionService,
        IFlowEngineShopifyReadResultFactory shopifyReadResultFactory,
        IFlowEngineShopifyOrderValidator shopifyOrderValidator,
        IFlowEngineShopifyOrderMapper shopifyOrderMapper,
        IFlowEngineJeevesBridgeService jeevesBridgeService,
        IFlowEngineShopifyQueryCatalog shopifyQueryCatalog)
    {
        _shopifyConnectionService = shopifyConnectionService;
        _shopifyGraphQlClient = shopifyGraphQlClient;
        _shopifyScopeProbeService = shopifyScopeProbeService;
        _shopifySelectionService = shopifySelectionService;
        _shopifyReadResultFactory = shopifyReadResultFactory;
        _shopifyOrderValidator = shopifyOrderValidator;
        _shopifyOrderMapper = shopifyOrderMapper;
        _jeevesBridgeService = jeevesBridgeService;
        _shopifyQueryCatalog = shopifyQueryCatalog;
    }

    public async Task<FlowEngineOperationExecutionData> ExecuteAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken = default)
    {
        var shopifyConnection = await _shopifyConnectionService.CreateAsync(runtimeContext.CompanyId, cancellationToken);
        var storeDomain = shopifyConnection.StoreDomain;
        var endpointUrl = shopifyConnection.EndpointUrl;
        var accessToken = shopifyConnection.AccessToken;

        var grantedScopes = request.Operation == FlowEngineOperationType.ShopifyScopesCheck
            ? await _shopifyScopeProbeService.ResolveGrantedScopesWithShopDetailsAsync(endpointUrl, accessToken, cancellationToken)
            : await _shopifyScopeProbeService.ResolveGrantedScopesAsync(endpointUrl, accessToken, cancellationToken);

        return request.Operation switch
        {
            FlowEngineOperationType.ShopifyScopesCheck => BuildScopesCheckExecution(grantedScopes, storeDomain),
            FlowEngineOperationType.ShopifyGetProducts => await ExecuteGetProductsAsync(
                runtimeContext,
                request,
                endpointUrl,
                accessToken,
                grantedScopes.Scopes,
                storeDomain,
                cancellationToken),
            FlowEngineOperationType.ShopifyFetchOrder => await ExecuteFetchOrderAsync(
                request,
                endpointUrl,
                accessToken,
                grantedScopes.Scopes,
                storeDomain,
                cancellationToken),
            FlowEngineOperationType.ShopifyFetchOrders => await ExecuteFetchOrdersAsync(
                request,
                endpointUrl,
                accessToken,
                grantedScopes.Scopes,
                storeDomain,
                cancellationToken),
            FlowEngineOperationType.ShopifyValidateOrder => await ExecuteValidateOrderAsync(
                request,
                endpointUrl,
                accessToken,
                grantedScopes.Scopes,
                storeDomain,
                cancellationToken),
            FlowEngineOperationType.ShopifyValidateOrders => await ExecuteValidateOrdersAsync(
                request,
                endpointUrl,
                accessToken,
                grantedScopes.Scopes,
                storeDomain,
                cancellationToken),
            FlowEngineOperationType.ShopifyCheckOrders => await ExecuteCheckOrdersAsync(
                runtimeContext,
                request,
                endpointUrl,
                accessToken,
                grantedScopes.Scopes,
                storeDomain,
                cancellationToken),
            FlowEngineOperationType.ShopifySendOrder => await ExecuteSendOrderAsync(
                runtimeContext,
                request,
                endpointUrl,
                accessToken,
                grantedScopes.Scopes,
                storeDomain,
                cancellationToken),
            FlowEngineOperationType.ShopifySendOrders => await ExecuteSendOrdersAsync(
                runtimeContext,
                request,
                endpointUrl,
                accessToken,
                grantedScopes.Scopes,
                storeDomain,
                cancellationToken),
            _ => throw new InvalidOperationException($"Operationen {request.Operation} stods inte av Shopify read-tjansten.")
        };
    }

    private FlowEngineOperationExecutionData BuildScopesCheckExecution(
        FlowEngineShopifyScopeProbeResult scopeProbe,
        string storeDomain)
    {
        var categories = _shopifyScopeProbeService.BuildCategories(scopeProbe.Scopes);
        return _shopifyReadResultFactory.BuildScopesCheckExecution(scopeProbe, categories, storeDomain);
    }

    private async Task<FlowEngineOperationExecutionData> ExecuteGetProductsAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        Uri endpointUrl,
        string accessToken,
        HashSet<string> grantedScopes,
        string storeDomain,
        CancellationToken cancellationToken)
    {
        _shopifyScopeProbeService.ValidateRequiredScopes("get-products", grantedScopes);

        var effectiveLimit = request.Params.UseLimit && request.Params.Limit.HasValue && request.Params.Limit.Value > 0
            ? request.Params.Limit.Value
            : 100;

        var updatedSince = _shopifySelectionService.ParseUpdatedSince(request.Params.ShopifyUpdatedSince);
        var searchQuery = _shopifySelectionService.BuildProductsSearchQuery(request.Params.ShopifyQuery, updatedSince);
        var includeInventoryItem = grantedScopes.Contains("read_inventory");
        var includeMetafields = grantedScopes.Contains("read_metafields");

        var pageResult = await CollectProductsAsync(
            endpointUrl,
            accessToken,
            searchQuery,
            effectiveLimit,
            DefaultPageSize,
            includeInventoryItem,
            includeMetafields,
            cancellationToken);

        var orderedProducts = pageResult.Products
            .OrderBy(product => product.UpdatedAt ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(product => product.LegacyResourceId ?? product.Id ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        return _shopifyReadResultFactory.BuildGetProductsExecution(
            runtimeContext.CompanyName,
            runtimeContext.CompanyCode,
            storeDomain,
            effectiveLimit,
            updatedSince,
            searchQuery,
            includeInventoryItem,
            includeMetafields,
            pageResult,
            orderedProducts);
    }

    private async Task<FlowEngineOperationExecutionData> ExecuteFetchOrderAsync(
        FlowEngineExecuteJobRequest request,
        Uri endpointUrl,
        string accessToken,
        HashSet<string> grantedScopes,
        string storeDomain,
        CancellationToken cancellationToken)
    {
        _shopifyScopeProbeService.ValidateRequiredScopes("fetch-order", grantedScopes);

        var orderGid = _shopifySelectionService.NormalizeOrderGid(request.Params.OrderId);
        var order = await FetchOrderAsync(endpointUrl, accessToken, orderGid, cancellationToken);
        if (order is null)
            throw new InvalidOperationException($"Shopify order hittades inte for {orderGid}.");

        var numericId = ResolveNumericId(order) ?? _shopifySelectionService.ExtractNumericIdFromGid(orderGid) ?? orderGid;
        return _shopifyReadResultFactory.BuildFetchOrderExecution(numericId, orderGid, order, storeDomain);
    }

    private async Task<FlowEngineOperationExecutionData> ExecuteFetchOrdersAsync(
        FlowEngineExecuteJobRequest request,
        Uri endpointUrl,
        string accessToken,
        HashSet<string> grantedScopes,
        string storeDomain,
        CancellationToken cancellationToken)
    {
        _shopifyScopeProbeService.ValidateRequiredScopes("fetch-orders", grantedScopes);
        var batch = ResolveDateBatchRequest(request, grantedScopes);

        var days = new List<object>();
        var totalOrders = 0;
        foreach (var date in batch.Dates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dayLabel = _shopifySelectionService.FormatDateUtc(date);
            var query = _shopifySelectionService.BuildDateSearchQuery(date);
            var orders = await CollectSummaryOrdersAsync(endpointUrl, accessToken, query, batch.Limit, cancellationToken);
            var ordered = orders
                .OrderBy(order => order.CreatedAt ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(order => ResolveNumericId(order) ?? order.Id ?? string.Empty, StringComparer.Ordinal)
                .ToList();

            totalOrders += ordered.Count;
            days.Add(new
            {
                date = dayLabel,
                query,
                orderCount = ordered.Count,
                hasNextPage = ordered.Count >= batch.Limit,
                orders = ordered
            });
        }

        var selectedDate = batch.Dates.Count == 1 ? _shopifySelectionService.FormatDateUtc(batch.Dates[0]) : null;
        var sinceUtc = _shopifySelectionService.FormatDateUtc(batch.Selection.SinceUtc);
        var untilUtc = _shopifySelectionService.FormatDateUtc(batch.Selection.UntilUtc);
        var useLatestDay = batch.Selection.SelectionKind == "latest-day";
        var selectionSummaryLabel = _shopifySelectionService.BuildSelectionSummaryLabel(batch.Selection.SelectionKind, selectedDate, sinceUtc, untilUtc);

        return _shopifyReadResultFactory.BuildFetchOrdersExecution(
            selectedDate,
            sinceUtc,
            untilUtc,
            useLatestDay,
            batch.Selection.SelectionKind,
            days,
            totalOrders,
            storeDomain,
            selectionSummaryLabel);
    }

    private async Task<FlowEngineOperationExecutionData> ExecuteValidateOrderAsync(
        FlowEngineExecuteJobRequest request,
        Uri endpointUrl,
        string accessToken,
        HashSet<string> grantedScopes,
        string storeDomain,
        CancellationToken cancellationToken)
    {
        _shopifyScopeProbeService.ValidateRequiredScopes("validate-order", grantedScopes);

        var orderGid = _shopifySelectionService.NormalizeOrderGid(request.Params.OrderId);
        var order = await FetchOrderAsync(endpointUrl, accessToken, orderGid, cancellationToken);
        if (order is null)
            throw new InvalidOperationException($"Shopify order hittades inte for {orderGid}.");

        var orderId = ResolveNumericId(order) ?? _shopifySelectionService.ExtractNumericIdFromGid(orderGid) ?? orderGid;
        var validation = ValidateOrder(order);
        return _shopifyReadResultFactory.BuildValidateOrderExecution(orderId, orderGid, validation, storeDomain);
    }

    private async Task<FlowEngineOperationExecutionData> ExecuteValidateOrdersAsync(
        FlowEngineExecuteJobRequest request,
        Uri endpointUrl,
        string accessToken,
        HashSet<string> grantedScopes,
        string storeDomain,
        CancellationToken cancellationToken)
    {
        _shopifyScopeProbeService.ValidateRequiredScopes("validate-orders", grantedScopes);
        var batch = ResolveDateBatchRequest(request, grantedScopes);

        var payload = _shopifyReadResultFactory.CreateValidateOrdersPayload(
            batch.Dates.Count == 1 ? _shopifySelectionService.FormatDateUtc(batch.Dates[0]) : null,
            _shopifySelectionService.FormatDateUtc(batch.Selection.SinceUtc),
            _shopifySelectionService.FormatDateUtc(batch.Selection.UntilUtc),
            batch.Selection.SelectionKind == "latest-day",
            batch.Selection.SelectionKind);

        foreach (var date in batch.Dates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dayLabel = _shopifySelectionService.FormatDateUtc(date);
            var query = _shopifySelectionService.BuildDateSearchQuery(date);
            var orders = await CollectDetailedOrdersAsync(
                endpointUrl,
                accessToken,
                query,
                batch.Limit,
                _shopifyQueryCatalog.ValidateOrdersByDateQuery,
                "ShopifyValidateOrdersByDate",
                cancellationToken);

            var day = _shopifyReadResultFactory.CreateValidateOrdersDay(dayLabel);

            foreach (var order in orders.OrderBy(order => order.CreatedAt ?? string.Empty, StringComparer.Ordinal)
                         .ThenBy(order => ResolveNumericId(order) ?? order.Id ?? string.Empty, StringComparer.Ordinal))
            {
                var decision = ValidateOrder(order);
                day.Orders.Add(new FlowEngineShopifyValidatedOrderRow
                {
                    OrderId = ResolveNumericId(order) ?? order.Id ?? "unknown",
                    OrderGid = order.Id,
                    Validation = decision
                });
            }

            day.Counts.Total = day.Orders.Count;
            day.Counts.Eligible = day.Orders.Count(order => order.Validation.Status == "eligible");
            day.Counts.Skipped = day.Orders.Count(order => order.Validation.Status == "skipped");
            day.Counts.Failed = day.Orders.Count(order => order.Validation.Status == "failed");
            payload.Days.Add(day);
        }

        return _shopifyReadResultFactory.BuildValidateOrdersExecution(
            payload,
            _shopifySelectionService.BuildSelectionSummaryLabel(payload.SelectionKind, payload.Date, payload.SinceUtc, payload.UntilUtc),
            storeDomain);
    }

    private async Task<FlowEngineOperationExecutionData> ExecuteCheckOrdersAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        Uri endpointUrl,
        string accessToken,
        HashSet<string> grantedScopes,
        string storeDomain,
        CancellationToken cancellationToken)
    {
        _shopifyScopeProbeService.ValidateRequiredScopes("check-orders", grantedScopes);

        var jeevesConfig = _jeevesBridgeService.ResolveConfig(runtimeContext.CompanyId, "Shopify check-orders");
        var batch = ResolveDateBatchRequest(request, grantedScopes);

        var payload = _shopifyReadResultFactory.CreateCheckOrdersPayload(
            batch.Dates.Count == 1 ? _shopifySelectionService.FormatDateUtc(batch.Dates[0]) : null,
            _shopifySelectionService.FormatDateUtc(batch.Selection.SinceUtc),
            _shopifySelectionService.FormatDateUtc(batch.Selection.UntilUtc),
            batch.Selection.SelectionKind == "latest-day",
            batch.Selection.SelectionKind);

        foreach (var date in batch.Dates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dayLabel = _shopifySelectionService.FormatDateUtc(date);
            var query = _shopifySelectionService.BuildDateSearchQuery(date);
            var orders = await CollectSummaryOrdersAsync(endpointUrl, accessToken, query, batch.Limit, cancellationToken);
            var day = _shopifyReadResultFactory.CreateCheckOrdersDay(dayLabel);

            foreach (var order in orders.OrderBy(order => order.CreatedAt ?? string.Empty, StringComparer.Ordinal)
                         .ThenBy(order => ResolveNumericId(order) ?? order.Id ?? string.Empty, StringComparer.Ordinal))
            {
                var reference = BuildReference(order);
                var row = new FlowEngineShopifyCheckedOrderRow
                {
                    OrderId = reference.NumericId ?? order.LegacyResourceId ?? order.Id ?? "unknown",
                    OrderGid = order.Id,
                    ExtOrderNr = reference.NumericId,
                    ShopifyFinancialStatus = order.DisplayFinancialStatus,
                    ShopifyFulfillmentStatus = order.DisplayFulfillmentStatus
                };

                if (string.IsNullOrWhiteSpace(reference.NumericId))
                {
                    row.Status = "failed_validation";
                    row.ErrorMessage = "numericId cannot be resolved from GID or legacyResourceId";
                    day.Orders.Add(row);
                    continue;
                }

                var check = await _jeevesBridgeService.CheckOrderAsync(runtimeContext.CompanyId, jeevesConfig, reference.NumericId, cancellationToken);
                row.JeevesOrderStatus = check.JeevesOrderStatus;
                row.JeevesOrderNumber = check.JeevesOrderNumber;
                row.JeevesStatusName = check.StatusName;
                row.ErrorMessage = check.ErrorMessage;
                row.Status = check.Status switch
                {
                    FlowEngineJeevesLookupStatus.Found => "found",
                    FlowEngineJeevesLookupStatus.NotFound => "missing",
                    FlowEngineJeevesLookupStatus.Error => "error",
                    _ => "error"
                };

                day.Orders.Add(row);
            }

            day.Counts.Total = day.Orders.Count;
            day.Counts.Found = day.Orders.Count(order => order.Status == "found");
            day.Counts.Missing = day.Orders.Count(order => order.Status == "missing");
            day.Counts.FailedValidation = day.Orders.Count(order => order.Status == "failed_validation");
            day.Counts.Error = day.Orders.Count(order => order.Status == "error");
            payload.Days.Add(day);
        }

        return _shopifyReadResultFactory.BuildCheckOrdersExecution(
            payload,
            _shopifySelectionService.BuildSelectionSummaryLabel(payload.SelectionKind, payload.Date, payload.SinceUtc, payload.UntilUtc),
            storeDomain);
    }

    private async Task<FlowEngineOperationExecutionData> ExecuteSendOrderAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        Uri endpointUrl,
        string accessToken,
        HashSet<string> grantedScopes,
        string storeDomain,
        CancellationToken cancellationToken)
    {
        _shopifyScopeProbeService.ValidateRequiredScopes("send-order", grantedScopes);

        var orderGid = _shopifySelectionService.NormalizeOrderGid(request.Params.OrderId);
        var order = await FetchOrderAsync(endpointUrl, accessToken, orderGid, cancellationToken);
        if (order is null)
            throw new InvalidOperationException($"Shopify order hittades inte for {orderGid}.");

        var jeevesConfig = _jeevesBridgeService.ResolveConfig(runtimeContext.CompanyId, "Shopify send-order");
        var outcome = await ProcessSendOrderAsync(
            runtimeContext.CompanyId,
            order,
            endpointUrl,
            accessToken,
            jeevesConfig,
            request.Flags.DryRun,
            request.Flags.SkipJeevesCheck,
            cancellationToken);

        return _shopifyReadResultFactory.BuildSendOrderExecution(
            storeDomain,
            request.Flags.DryRun,
            request.Flags.SkipJeevesCheck,
            outcome.OrderId,
            outcome.OrderGid,
            outcome.Status,
            outcome.Validation,
            outcome.Payload,
            outcome.ErrorMessage);
    }

    private async Task<FlowEngineOperationExecutionData> ExecuteSendOrdersAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        Uri endpointUrl,
        string accessToken,
        HashSet<string> grantedScopes,
        string storeDomain,
        CancellationToken cancellationToken)
    {
        _shopifyScopeProbeService.ValidateRequiredScopes("send-orders", grantedScopes);
        var batch = ResolveDateBatchRequest(request, grantedScopes);

        var jeevesConfig = _jeevesBridgeService.ResolveConfig(runtimeContext.CompanyId, "Shopify send-orders");
        var payload = _shopifyReadResultFactory.CreateSendOrdersPayload(
            batch.Dates.Count == 1 ? _shopifySelectionService.FormatDateUtc(batch.Dates[0]) : null,
            _shopifySelectionService.FormatDateUtc(batch.Selection.SinceUtc),
            _shopifySelectionService.FormatDateUtc(batch.Selection.UntilUtc),
            batch.Selection.SelectionKind == "latest-day",
            batch.Selection.SelectionKind,
            request.Flags.DryRun,
            request.Flags.SkipJeevesCheck);

        foreach (var date in batch.Dates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();
            var dayLabel = _shopifySelectionService.FormatDateUtc(date);
            var query = _shopifySelectionService.BuildDateSearchQuery(date);
            var orders = await CollectDetailedOrdersAsync(
                endpointUrl,
                accessToken,
                query,
                batch.Limit,
                _shopifyQueryCatalog.ValidateOrdersByDateQuery,
                "ShopifyValidateOrdersByDate",
                cancellationToken);

            var day = _shopifyReadResultFactory.CreateSendOrdersDay(dayLabel);

            foreach (var order in orders.OrderBy(item => item.CreatedAt ?? string.Empty, StringComparer.Ordinal)
                         .ThenBy(item => ResolveNumericId(item) ?? item.Id ?? string.Empty, StringComparer.Ordinal))
            {
                var outcome = await ProcessSendOrderAsync(
                    runtimeContext.CompanyId,
                    order,
                    endpointUrl,
                    accessToken,
                    jeevesConfig,
                    request.Flags.DryRun,
                    request.Flags.SkipJeevesCheck,
                    cancellationToken);

                _shopifyReadResultFactory.AddSendOrderOutcome(
                    day,
                    outcome.OrderId,
                    outcome.OrderNumber,
                    outcome.OrderGid,
                    outcome.Status,
                    outcome.Validation,
                    outcome.ErrorMessage);
            }

            stopwatch.Stop();
            day.RuntimeSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 2);
            payload.Days.Add(day);
            MergeSendCounts(payload.Counts, day.Counts);
        }

        return _shopifyReadResultFactory.BuildSendOrdersExecution(
            payload,
            _shopifySelectionService.BuildSelectionSummaryLabel(payload.SelectionKind, payload.Date, payload.SinceUtc, payload.UntilUtc),
            storeDomain);
    }

    private async Task<ShopifySendOutcome> ProcessSendOrderAsync(
        Guid companyId,
        ShopifyOrderDetailNode order,
        Uri endpointUrl,
        string accessToken,
        IntegrationSourceConfig jeevesConfig,
        bool dryRun,
        bool skipJeevesCheck,
        CancellationToken cancellationToken)
    {
        var reference = BuildReference(order);
        var orderId = reference.NumericId ?? order.LegacyResourceId ?? _shopifySelectionService.ExtractNumericIdFromGid(order.Id) ?? "unknown";
        var orderGid = order.Id;
        var decision = ValidateOrder(order);

        if (decision.Status == "failed")
        {
            return new ShopifySendOutcome(
                orderId,
                order.Name,
                orderGid,
                "failed",
                decision,
                null,
                decision.Message);
        }

        if (decision.Status == "skipped")
        {
            return new ShopifySendOutcome(
                orderId,
                order.Name,
                orderGid,
                "skipped_ineligible",
                decision,
                null,
                decision.Message);
        }

        FlowEngineShopifyJeevesOrderPayload mapped;
        try
        {
            mapped = MapToJeevesOrder(order);
        }
        catch (Exception ex)
        {
            var failedDecision = FailedDecision("SHP-VAL-012", ex.Message, "Configure mapping and rerun validation");
            return new ShopifySendOutcome(orderId, order.Name, orderGid, "failed", failedDecision, null, ex.Message);
        }

        try
        {
            var status = "mapped";
            string? message = null;

            if (!skipJeevesCheck && !dryRun)
            {
                var exists = await _jeevesBridgeService.OrderExistsAsync(jeevesConfig, companyId, mapped.CompanyCode, mapped.ExternalOrderNumber, cancellationToken);
                if (exists)
                {
                    status = "skipped_existing";
                    message = "Order already exists in Jeeves (lookup by c_foretagkod + c_extordernr)";
                }
            }

            if (status == "mapped" && !dryRun)
            {
                try
                {
                    await _jeevesBridgeService.CreateOrderAsync(jeevesConfig, companyId, mapped, cancellationToken);
                    status = "sent";
                }
                catch (FlowEngineJeevesDuplicateOrderException)
                {
                    status = "skipped_existing";
                    message = "Order already exists in Jeeves (duplicate in OHDEDI primary key)";
                }
            }

            if (!dryRun &&
                (string.Equals(status, "sent", StringComparison.Ordinal) || string.Equals(status, "skipped_existing", StringComparison.Ordinal)) &&
                !string.IsNullOrWhiteSpace(orderGid))
            {
                try
                {
                    await TryAddTagAsync(endpointUrl, accessToken, orderGid!, ShopifyJeevesSyncedTag, cancellationToken);
                }
                catch
                {
                    // Tagging is informational and should not fail the send.
                }
            }

            return new ShopifySendOutcome(orderId, order.Name, orderGid, status, decision, mapped, message);
        }
        catch (Exception ex)
        {
            return new ShopifySendOutcome(orderId, order.Name, orderGid, "failed", decision, null, ex.Message);
        }
    }

    private async Task<FlowEngineShopifyCollectProductsResult> CollectProductsAsync(
        Uri endpointUrl,
        string accessToken,
        string? query,
        int limit,
        int pageSize,
        bool includeInventoryItem,
        bool includeMetafields,
        CancellationToken cancellationToken)
    {
        var safePageSize = Math.Max(1, pageSize);
        var products = new List<ShopifyProductNode>();
        string? cursor = null;
        bool hasNextPage;
        string? endCursor;
        var queryDocument = _shopifyQueryCatalog.BuildGetProductsQuery(includeInventoryItem, includeMetafields);

        do
        {
            var fetchCount = Math.Min(safePageSize, limit - products.Count);
            var variables = new Dictionary<string, object?>
            {
                ["first"] = fetchCount
            };
            if (!string.IsNullOrWhiteSpace(cursor))
                variables["after"] = cursor;
            if (!string.IsNullOrWhiteSpace(query))
                variables["query"] = query;

            var response = await PostGraphQlAsync<ShopifyGetProductsData>(
                endpointUrl,
                accessToken,
                queryDocument,
                variables,
                "ShopifyGetProducts",
                cancellationToken);

            products.AddRange(response.Products?.Edges?.Select(edge => edge.Node!).Where(node => node is not null) ?? Enumerable.Empty<ShopifyProductNode>());
            hasNextPage = response.Products?.PageInfo?.HasNextPage ?? false;
            endCursor = response.Products?.PageInfo?.EndCursor;
            cursor = hasNextPage && !string.IsNullOrWhiteSpace(endCursor) ? endCursor : null;
        } while (products.Count < limit && !string.IsNullOrWhiteSpace(cursor));

        return new FlowEngineShopifyCollectProductsResult(products.Take(limit).ToList(), hasNextPage, endCursor);
    }

    private async Task<List<ShopifyOrderSummaryNode>> CollectSummaryOrdersAsync(
        Uri endpointUrl,
        string accessToken,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        var orders = new List<ShopifyOrderSummaryNode>();
        string? cursor = null;
        do
        {
            var fetchCount = Math.Min(DefaultPageSize, limit - orders.Count);
            var variables = new Dictionary<string, object?>
            {
                ["first"] = fetchCount,
                ["query"] = query
            };
            if (!string.IsNullOrWhiteSpace(cursor))
                variables["after"] = cursor;

            var response = await PostGraphQlAsync<ShopifyFetchOrdersData>(
                endpointUrl,
                accessToken,
                _shopifyQueryCatalog.FetchOrdersByDateQuery,
                variables,
                "ShopifyFetchOrdersByDate",
                cancellationToken);

            orders.AddRange(response.Orders?.Edges?.Select(edge => edge.Node!).Where(node => node is not null) ?? Enumerable.Empty<ShopifyOrderSummaryNode>());
            var pageInfo = response.Orders?.PageInfo;
            cursor = pageInfo?.HasNextPage == true && !string.IsNullOrWhiteSpace(pageInfo.EndCursor) ? pageInfo.EndCursor : null;
        } while (orders.Count < limit && !string.IsNullOrWhiteSpace(cursor));

        return orders.Take(limit).ToList();
    }

    private async Task<List<ShopifyOrderDetailNode>> CollectDetailedOrdersAsync(
        Uri endpointUrl,
        string accessToken,
        string query,
        int limit,
        string queryDocument,
        string operationName,
        CancellationToken cancellationToken)
    {
        var orders = new List<ShopifyOrderDetailNode>();
        string? cursor = null;
        do
        {
            var fetchCount = Math.Min(DefaultPageSize, limit - orders.Count);
            var variables = new Dictionary<string, object?>
            {
                ["first"] = fetchCount,
                ["query"] = query
            };
            if (!string.IsNullOrWhiteSpace(cursor))
                variables["after"] = cursor;

            var response = await PostGraphQlAsync<ShopifyValidateOrdersByDateData>(
                endpointUrl,
                accessToken,
                queryDocument,
                variables,
                operationName,
                cancellationToken);

            orders.AddRange(response.Orders?.Edges?.Select(edge => edge.Node!).Where(node => node is not null) ?? Enumerable.Empty<ShopifyOrderDetailNode>());
            var pageInfo = response.Orders?.PageInfo;
            cursor = pageInfo?.HasNextPage == true && !string.IsNullOrWhiteSpace(pageInfo.EndCursor) ? pageInfo.EndCursor : null;
        } while (orders.Count < limit && !string.IsNullOrWhiteSpace(cursor));

        return orders.Take(limit).ToList();
    }

    private async Task<ShopifyOrderDetailNode?> FetchOrderAsync(
        Uri endpointUrl,
        string accessToken,
        string orderGid,
        CancellationToken cancellationToken)
    {
        var response = await PostGraphQlAsync<ShopifyFetchOrderData>(
            endpointUrl,
            accessToken,
            _shopifyQueryCatalog.FetchOrderQuery,
            new Dictionary<string, object?> { ["id"] = orderGid },
            "ShopifyFetchOrder",
            cancellationToken);

        return response.Order;
    }

    private FlowEngineShopifyValidationDecision ValidateOrder(ShopifyOrderDetailNode order)
    {
        var shipping = ResolveShippingInfo(order);
        return _shopifyOrderValidator.Validate(new FlowEngineShopifyOrderValidationInput
        {
            NumericId = BuildReference(order).NumericId,
            IsCancelled = !string.IsNullOrWhiteSpace(order.CancelledAt),
            IsTest = order.Test == true,
            DisplayFinancialStatus = order.DisplayFinancialStatus,
            DisplayFulfillmentStatus = order.DisplayFulfillmentStatus,
            CustomerFirstName = order.Customer?.FirstName,
            CustomerLastName = order.Customer?.LastName,
            CustomerEmail = order.Customer?.Email,
            CustomerPhone = order.Customer?.Phone,
            ShippingAddress = MapAddress(order.ShippingAddress),
            BillingAddress = MapAddress(order.BillingAddress),
            HasLineWithoutSku = (order.LineItems?.Edges ?? new List<ShopifyOrderLineItemEdge>())
                .Any(edge => string.IsNullOrWhiteSpace(edge.Node?.Sku) && string.IsNullOrWhiteSpace(edge.Node?.Variant?.Sku)),
            ShippingAmount = shipping.Amount,
            ShippingCurrencyCode = shipping.CurrencyCode
        });
    }

    private FlowEngineShopifyJeevesOrderPayload MapToJeevesOrder(ShopifyOrderDetailNode order)
        => _shopifyOrderMapper.MapToJeevesOrder(MapOrderMappingInput(order));

    private static ShippingInfo ResolveShippingInfo(ShopifyOrderDetailNode order)
    {
        decimal total = 0;
        var hasTotal = false;
        string? currencyCode = null;
        foreach (var edge in order.ShippingLines?.Edges ?? new List<ShopifyShippingLineEdge>())
        {
            var currentDiscounted = edge.Node?.CurrentDiscountedPriceSet?.ShopMoney;
            var discounted = edge.Node?.DiscountedPriceSet?.ShopMoney;
            var original = edge.Node?.OriginalPriceSet?.ShopMoney;
            var amount = ParseDecimal(currentDiscounted?.Amount) ?? ParseDecimal(discounted?.Amount) ?? ParseDecimal(original?.Amount);
            if (amount.HasValue)
            {
                total += amount.Value;
                hasTotal = true;
            }

            currencyCode ??= Normalize(currentDiscounted?.CurrencyCode) ?? Normalize(discounted?.CurrencyCode) ?? Normalize(original?.CurrencyCode);
        }

        if (hasTotal)
            return new ShippingInfo(total, currencyCode);

        return new ShippingInfo(
            ParseDecimal(order.TotalShippingPriceSet?.ShopMoney?.Amount),
            Normalize(order.TotalShippingPriceSet?.ShopMoney?.CurrencyCode));
    }

    private static FlowEngineShopifyAddressValidationInput? MapAddress(ShopifyAddressNode? address)
    {
        if (address is null)
            return null;

        return new FlowEngineShopifyAddressValidationInput
        {
            FirstName = address.FirstName,
            LastName = address.LastName,
            Address1 = address.Address1,
            City = address.City,
            Zip = address.Zip,
            CountryCodeV2 = address.CountryCodeV2
        };
    }

    private FlowEngineShopifyOrderMappingInput MapOrderMappingInput(ShopifyOrderDetailNode order)
    {
        return new FlowEngineShopifyOrderMappingInput
        {
            NumericId = BuildReference(order).NumericId,
            Name = order.Name,
            CreatedAt = order.CreatedAt,
            CustomerFirstName = order.Customer?.FirstName,
            CustomerLastName = order.Customer?.LastName,
            CustomerEmail = order.Customer?.Email,
            CustomerPhone = order.Customer?.Phone,
            ShippingAddress = MapOrderAddress(order.ShippingAddress),
            BillingAddress = MapOrderAddress(order.BillingAddress),
            FallbackShippingAmount = ParseDecimal(order.TotalShippingPriceSet?.ShopMoney?.Amount),
            FallbackShippingCurrencyCode = Normalize(order.TotalShippingPriceSet?.ShopMoney?.CurrencyCode),
            ShippingLines = (order.ShippingLines?.Edges ?? new List<ShopifyShippingLineEdge>())
                .Select(edge => new FlowEngineShopifyShippingLineMappingInput
                {
                    CurrentDiscountedAmount = ParseDecimal(edge.Node?.CurrentDiscountedPriceSet?.ShopMoney?.Amount),
                    CurrentDiscountedCurrencyCode = edge.Node?.CurrentDiscountedPriceSet?.ShopMoney?.CurrencyCode,
                    DiscountedAmount = ParseDecimal(edge.Node?.DiscountedPriceSet?.ShopMoney?.Amount),
                    DiscountedCurrencyCode = edge.Node?.DiscountedPriceSet?.ShopMoney?.CurrencyCode,
                    OriginalAmount = ParseDecimal(edge.Node?.OriginalPriceSet?.ShopMoney?.Amount),
                    OriginalCurrencyCode = edge.Node?.OriginalPriceSet?.ShopMoney?.CurrencyCode
                })
                .ToList(),
            OrderLines = (order.LineItems?.Edges ?? new List<ShopifyOrderLineItemEdge>())
                .Select(edge => new FlowEngineShopifyOrderLineMappingInput
                {
                    Sku = edge.Node?.Sku,
                    VariantSku = edge.Node?.Variant?.Sku,
                    Quantity = edge.Node?.Quantity ?? 0,
                    DiscountedTotalAmount = ParseDecimal(edge.Node?.DiscountedTotalSet?.ShopMoney?.Amount),
                    DiscountedTotalCurrencyCode = edge.Node?.DiscountedTotalSet?.ShopMoney?.CurrencyCode,
                    OriginalTotalAmount = ParseDecimal(edge.Node?.OriginalTotalSet?.ShopMoney?.Amount),
                    OriginalTotalCurrencyCode = edge.Node?.OriginalTotalSet?.ShopMoney?.CurrencyCode
                })
                .ToList()
        };
    }

    private static FlowEngineShopifyOrderAddressMappingInput? MapOrderAddress(ShopifyAddressNode? address)
    {
        if (address is null)
            return null;

        return new FlowEngineShopifyOrderAddressMappingInput
        {
            FirstName = address.FirstName,
            LastName = address.LastName,
            Company = address.Company,
            Address1 = address.Address1,
            Address2 = address.Address2,
            City = address.City,
            Zip = address.Zip,
            CountryCodeV2 = address.CountryCodeV2,
            Phone = address.Phone
        };
    }

    private async Task TryAddTagAsync(
        Uri endpointUrl,
        string accessToken,
        string orderGid,
        string tag,
        CancellationToken cancellationToken)
    {
        var response = await PostGraphQlAsync<ShopifyTagsAddData>(
            endpointUrl,
            accessToken,
            _shopifyQueryCatalog.TagsAddMutation,
            new Dictionary<string, object?>
            {
                ["id"] = orderGid,
                ["tags"] = new[] { tag }
            },
            "ShopifyTagsAdd",
            cancellationToken);

        var userErrors = response.TagsAdd?.UserErrors?
                             .Select(error => Normalize(error.Message))
                             .Where(message => message is not null)
                             .Select(message => message!)
                             .ToList()
                         ?? new List<string>();
        if (userErrors.Count > 0)
            throw new InvalidOperationException($"Shopify tagsAdd failed: {string.Join(" | ", userErrors)}");
    }

    private Task<T> PostGraphQlAsync<T>(
        Uri endpointUrl,
        string accessToken,
        string query,
        Dictionary<string, object?> variables,
        string operationName,
        CancellationToken cancellationToken)
        where T : class
        => _shopifyGraphQlClient.PostAsync<T>(endpointUrl, accessToken, query, variables, operationName, cancellationToken);

    private string? ResolveNumericId(ShopifyOrderSummaryNode order)
        => !string.IsNullOrWhiteSpace(order.LegacyResourceId) ? order.LegacyResourceId.Trim() : _shopifySelectionService.ExtractNumericIdFromGid(order.Id);

    private string? ResolveNumericId(ShopifyOrderDetailNode order)
        => !string.IsNullOrWhiteSpace(order.LegacyResourceId) ? order.LegacyResourceId.Trim() : _shopifySelectionService.ExtractNumericIdFromGid(order.Id);

    private FlowEngineShopifyOrderReference BuildReference(ShopifyOrderSummaryNode order)
        => new(order.Id, ResolveNumericId(order));

    private FlowEngineShopifyOrderReference BuildReference(ShopifyOrderDetailNode order)
        => new(order.Id, ResolveNumericId(order));

    private static decimal? ParseDecimal(string? value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static void MergeSendCounts(FlowEngineShopifySendCounts target, FlowEngineShopifySendCounts source)
    {
        target.Total += source.Total;
        target.Mapped += source.Mapped;
        target.Sent += source.Sent;
        target.SkippedExisting += source.SkippedExisting;
        target.SkippedIneligible += source.SkippedIneligible;
        target.Failed += source.Failed;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private ShopifyDateBatchRequest ResolveDateBatchRequest(
        FlowEngineExecuteJobRequest request,
        HashSet<string> grantedScopes)
    {
        var selection = _shopifySelectionService.ResolveDateSelection(request);
        if (_shopifySelectionService.RequiresReadAllOrders(selection.SinceUtc) && !grantedScopes.Contains("read_all_orders"))
            throw new InvalidOperationException("Shopify saknar read_all_orders for aldre datumintervall.");

        var dates = _shopifySelectionService.EnumerateDates(selection.SinceUtc, selection.UntilUtc);
        if (selection.SelectionKind == "range" && dates.Count > 7 && !request.Flags.ForceRange)
            throw new InvalidOperationException($"Shopify range ar {dates.Count} dagar. Anvand Force range for att overskrida 7 dagar.");

        return new ShopifyDateBatchRequest(
            selection,
            dates,
            ResolveEffectiveLimit(request));
    }

    private static int ResolveEffectiveLimit(FlowEngineExecuteJobRequest request)
        => request.Params.UseLimit && request.Params.Limit.HasValue && request.Params.Limit.Value > 0
            ? request.Params.Limit.Value
            : int.MaxValue;

    private static FlowEngineShopifyValidationDecision FailedDecision(string ruleId, string message, string remediation)
        => new()
        {
            Status = "failed",
            RuleId = ruleId,
            Classification = "failed",
            Message = message,
            Remediation = remediation
        };

    private sealed record ShippingInfo(decimal? Amount, string? CurrencyCode);
    private sealed record ShopifyDateBatchRequest(
        FlowEngineShopifyDateSelection Selection,
        IReadOnlyList<DateTime> Dates,
        int Limit);
    private sealed record ShopifySendOutcome(
        string OrderId,
        string? OrderNumber,
        string? OrderGid,
        string Status,
        FlowEngineShopifyValidationDecision? Validation,
        FlowEngineShopifyJeevesOrderPayload? Payload,
        string? ErrorMessage);
}

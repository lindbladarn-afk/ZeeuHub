using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.Services.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineShopifyCompleteOrdersService : IFlowEngineShopifyCompleteOrdersService
{
    private const int DefaultPageSize = 50;
    private const int PendingLookbackDays = 7;
    private const string JeevesSyncedTag = "SentToJeeves";
    private const string ShippedTag = "Shipped";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<IntegrationOptions> _integrationOptions;
    private readonly IJeevesAuthService _jeevesAuthService;
    private readonly IFlowEngineShopifyConnectionService _shopifyConnectionService;
    private readonly IFlowEngineShopifyGraphQlClient _shopifyGraphQlClient;
    private readonly IFlowEngineShopifyFulfillmentService _shopifyFulfillmentService;
    private readonly IFlowEngineShopifySelectionService _shopifySelectionService;
    private readonly IFlowEngineShopifyCompleteOrdersResultFactory _shopifyCompleteOrdersResultFactory;
    private readonly IFlowEngineShopifyQueryCatalog _shopifyQueryCatalog;

    public FlowEngineShopifyCompleteOrdersService(
        IHttpClientFactory httpClientFactory,
        IOptions<IntegrationOptions> integrationOptions,
        IJeevesAuthService jeevesAuthService,
        IFlowEngineShopifyConnectionService shopifyConnectionService,
        IFlowEngineShopifyGraphQlClient shopifyGraphQlClient,
        IFlowEngineShopifyFulfillmentService shopifyFulfillmentService,
        IFlowEngineShopifySelectionService shopifySelectionService,
        IFlowEngineShopifyCompleteOrdersResultFactory shopifyCompleteOrdersResultFactory,
        IFlowEngineShopifyQueryCatalog shopifyQueryCatalog)
    {
        _httpClientFactory = httpClientFactory;
        _integrationOptions = integrationOptions;
        _jeevesAuthService = jeevesAuthService;
        _shopifyConnectionService = shopifyConnectionService;
        _shopifyGraphQlClient = shopifyGraphQlClient;
        _shopifyFulfillmentService = shopifyFulfillmentService;
        _shopifySelectionService = shopifySelectionService;
        _shopifyCompleteOrdersResultFactory = shopifyCompleteOrdersResultFactory;
        _shopifyQueryCatalog = shopifyQueryCatalog;
    }

    public async Task<FlowEngineOperationExecutionData> ExecuteAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Operation is not (FlowEngineOperationType.CompleteOrdersPending or FlowEngineOperationType.CompleteOrders or FlowEngineOperationType.CompleteOrder))
            throw new InvalidOperationException($"Operationen {request.Operation} stods inte av Shopify complete orders-tjansten.");

        var isSingleOrderMode = request.Operation == FlowEngineOperationType.CompleteOrder;
        var isPendingMode = request.Operation == FlowEngineOperationType.CompleteOrdersPending;
        var closeOrder = request.Flags.CloseOrder;
        var dryRun = request.Flags.DryRun;
        var limit = request.Params.UseLimit && request.Params.Limit.HasValue && request.Params.Limit.Value > 0
            ? request.Params.Limit.Value
            : int.MaxValue;

        var jeevesConfig = ResolveConfig(runtimeContext.CompanyId, IntegrationSource.Jeeves);
        var shopifyConnection = await _shopifyConnectionService.CreateAsync(runtimeContext.CompanyId, cancellationToken);
        var storeDomain = shopifyConnection.StoreDomain;
        var endpointUrl = shopifyConnection.EndpointUrl;
        var accessToken = shopifyConnection.AccessToken;

        if (isSingleOrderMode)
        {
            var orderGid = _shopifySelectionService.NormalizeOrderGid(request.Params.OrderId);
            var fetchedOrder = await FetchOrderAsync(endpointUrl, accessToken, orderGid, cancellationToken);
            if (fetchedOrder is null)
                throw new InvalidOperationException($"Shopify order hittades inte for {orderGid}.");

            var row = await ProcessOrderAsync(runtimeContext, fetchedOrder, endpointUrl, accessToken, jeevesConfig, dryRun, closeOrder, cancellationToken);
            var singlePayload = _shopifyCompleteOrdersResultFactory.CreateSinglePayload(
                ResolveNumericId(fetchedOrder) ?? _shopifySelectionService.ExtractNumericIdFromGid(orderGid) ?? orderGid,
                orderGid,
                dryRun,
                closeOrder,
                row);

            return _shopifyCompleteOrdersResultFactory.BuildSingleOrderExecution(
                singlePayload,
                runtimeContext.CompanyName,
                runtimeContext.CompanyCode,
                storeDomain);
        }

        var operationLabel = isPendingMode ? "Shopify complete orders pending" : "Shopify complete orders";
        var modeLabel = isPendingMode ? "complete orders pending" : "complete orders";
        var payload = _shopifyCompleteOrdersResultFactory.CreateBulkPayload(
            null,
            null,
            null,
            false,
            string.Empty,
            dryRun,
            closeOrder);

        if (isPendingMode)
        {
            var pendingDate = $"pending-{DateTime.UtcNow:yyyy-MM-dd}";
            var day = await ExecuteDayAsync(
                runtimeContext,
                endpointUrl,
                accessToken,
                jeevesConfig,
                BuildPendingCompletionSearchQuery(),
                pendingDate,
                limit,
                dryRun,
                closeOrder,
                cancellationToken);

            payload.Date = pendingDate;
            payload.SelectionKind = "pending";
            payload.Days.Add(day);
            payload.Orders.AddRange(day.Orders);
            _shopifyCompleteOrdersResultFactory.MergeCounts(payload.Counts, day.Counts);
        }
        else
        {
            var selection = _shopifySelectionService.ResolveDateSelection(request);
            var dates = _shopifySelectionService.EnumerateDates(selection.SinceUtc, selection.UntilUtc);
            if (selection.SelectionKind == "range" && dates.Count > 7 && !request.Flags.ForceRange)
                throw new InvalidOperationException($"Shopify range ar {dates.Count} dagar. Anvand Force range for att overskrida 7 dagar.");

            payload.Date = dates.Count == 1 ? _shopifySelectionService.FormatDateUtc(dates[0]) : null;
            payload.SinceUtc = _shopifySelectionService.FormatDateUtc(selection.SinceUtc);
            payload.UntilUtc = _shopifySelectionService.FormatDateUtc(selection.UntilUtc);
            payload.UseLatestDay = selection.SelectionKind == "latest-day";
            payload.SelectionKind = selection.SelectionKind;
            payload.DryRun = dryRun;
            payload.CloseOrder = closeOrder;

            foreach (var date in dates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dayLabel = _shopifySelectionService.FormatDateUtc(date);
                var day = await ExecuteDayAsync(
                    runtimeContext,
                    endpointUrl,
                    accessToken,
                    jeevesConfig,
                    _shopifySelectionService.BuildDateSearchQuery(date),
                    dayLabel,
                    limit,
                    dryRun,
                    closeOrder,
                    cancellationToken);

                payload.Days.Add(day);
                payload.Orders.AddRange(day.Orders);
                _shopifyCompleteOrdersResultFactory.MergeCounts(payload.Counts, day.Counts);
            }
        }

        return _shopifyCompleteOrdersResultFactory.BuildBulkExecution(
            payload,
            operationLabel,
            modeLabel,
            runtimeContext.CompanyName,
            runtimeContext.CompanyCode,
            storeDomain);
    }

    private async Task<FlowEngineShopifyCompleteOrdersDayPayload> ExecuteDayAsync(
        JeevesRuntimeContext runtimeContext,
        Uri endpointUrl,
        string accessToken,
        IntegrationSourceConfig jeevesConfig,
        string searchQuery,
        string dayLabel,
        int limit,
        bool dryRun,
        bool closeOrder,
        CancellationToken cancellationToken)
    {
        var fetchedOrders = await CollectOrdersAsync(endpointUrl, accessToken, searchQuery, limit, cancellationToken);
        var ordered = fetchedOrders
            .OrderBy(order => order.CreatedAt ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(order => ResolveNumericId(order) ?? order.LegacyResourceId ?? order.Id ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        var dayPayload = new FlowEngineShopifyCompleteOrdersDayPayload
        {
            Date = dayLabel
        };

        foreach (var order in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = await ProcessOrderAsync(runtimeContext, order, endpointUrl, accessToken, jeevesConfig, dryRun, closeOrder, cancellationToken);
            dayPayload.Orders.Add(row);
            _shopifyCompleteOrdersResultFactory.IncrementCounts(dayPayload.Counts, row.Status);
        }

        dayPayload.Counts.Total = dayPayload.Orders.Count;
        return dayPayload;
    }

    private async Task<FlowEngineShopifyCompleteOrderRow> ProcessOrderAsync(
        JeevesRuntimeContext runtimeContext,
        ShopifyOrderNode order,
        Uri endpointUrl,
        string accessToken,
        IntegrationSourceConfig jeevesConfig,
        bool dryRun,
        bool closeOrder,
        CancellationToken cancellationToken)
    {
        var reference = BuildReference(order);
        var orderId = reference.NumericId ?? order.LegacyResourceId ?? "unknown";
        var orderGid = order.Id;

        if (string.IsNullOrWhiteSpace(reference.NumericId))
        {
            return FailedRow(
                orderId,
                orderGid,
                "SHP-CMP-002",
                "numericId cannot be resolved from GID or legacyResourceId",
                "Ensure order payload includes valid gid://shopify/Order/<id> or legacyResourceId",
                validationFailure: true);
        }

        if (!string.IsNullOrWhiteSpace(order.CancelledAt))
        {
            return SkippedRow(
                orderId,
                orderGid,
                "skipped_ineligible",
                "SHP-CMP-006",
                "Order is cancelled",
                "Exclude cancelled orders from complete commands");
        }

        if (order.Test == true)
        {
            return SkippedRow(
                orderId,
                orderGid,
                "skipped_ineligible",
                "SHP-CMP-007",
                "Order is marked as test",
                "Exclude test orders from completion");
        }

        var fulfillmentStatus = NormalizeState(order.DisplayFulfillmentStatus);
        if (fulfillmentStatus == "FULFILLED")
        {
            return SkippedRow(
                orderId,
                orderGid,
                "skipped_already_complete",
                "SHP-CMP-015",
                "Order is already fulfilled in Shopify",
                "No action required");
        }

        if (fulfillmentStatus != "UNFULFILLED")
        {
            return SkippedRow(
                orderId,
                orderGid,
                "skipped_ineligible",
                "SHP-CMP-008",
                "Order is not completion-eligible (requires displayFulfillmentStatus=UNFULFILLED)",
                "Complete only orders that are still unfulfilled in Shopify");
        }

        JeevesCheckResult jeevesCheck;
        try
        {
            jeevesCheck = await CheckJeevesOrderAsync(runtimeContext, jeevesConfig, reference.NumericId!, cancellationToken);
        }
        catch (Exception ex)
        {
            return FailedRow(
                orderId,
                orderGid,
                "SHP-CMP-012",
                $"Jeeves check failed: {ex.Message}",
                "Retry completion and verify Shopify/Jeeves connectivity");
        }

        if (jeevesCheck.Status == JeevesLookupStatus.NotFound)
        {
            return SkippedRow(
                orderId,
                orderGid,
                "skipped_ineligible",
                "SHP-CMP-013",
                "Jeeves order not found; completion requires c_ordstat >= 50",
                "Ensure order is sent and available in Jeeves before completion",
                jeevesCheck.JeevesOrderStatus);
        }

        if (jeevesCheck.Status == JeevesLookupStatus.Error)
        {
            return FailedRow(
                orderId,
                orderGid,
                "SHP-CMP-012",
                jeevesCheck.ErrorMessage ?? "Jeeves check failed",
                "Retry completion and verify Jeeves connectivity",
                false,
                jeevesCheck.JeevesOrderStatus);
        }

        if (jeevesCheck.JeevesOrderStatus < 50)
        {
            return SkippedRow(
                orderId,
                orderGid,
                "skipped_ineligible",
                "SHP-CMP-013",
                "Skipped: Jeeves c_ordstat must be >= 50",
                "Wait until Jeeves order reaches c_ordstat >= 50 before completion",
                jeevesCheck.JeevesOrderStatus);
        }

        if (string.IsNullOrWhiteSpace(orderGid))
        {
            return FailedRow(
                orderId,
                null,
                "SHP-CMP-001",
                "Order GID is missing in Shopify payload",
                "Refetch order and retry completion",
                true,
                jeevesCheck.JeevesOrderStatus);
        }

        List<string> fulfillmentOrderIds;
        try
        {
            fulfillmentOrderIds = await _shopifyFulfillmentService.CollectActionableFulfillmentOrderIdsAsync(endpointUrl, accessToken, orderGid, cancellationToken);
        }
        catch (Exception ex)
        {
            return FailedRow(
                orderId,
                orderGid,
                "SHP-CMP-012",
                ex.Message,
                "Retry completion and verify GraphQL operation permissions",
                false,
                jeevesCheck.JeevesOrderStatus);
        }

        if (fulfillmentOrderIds.Count == 0)
        {
            return SkippedRow(
                orderId,
                orderGid,
                "skipped_already_complete",
                "SHP-CMP-015",
                "No actionable fulfillment orders found",
                "No action required",
                jeevesCheck.JeevesOrderStatus);
        }

        if (dryRun)
        {
            return new FlowEngineShopifyCompleteOrderRow
            {
                OrderId = orderId,
                OrderGid = orderGid,
                Status = "ready",
                Validation = EligibleDecision(),
                FulfillmentOrderIds = fulfillmentOrderIds,
                JeevesOrderStatus = jeevesCheck.JeevesOrderStatus
            };
        }

        var createResult = await _shopifyFulfillmentService.CreateFulfillmentAsync(
            endpointUrl,
            accessToken,
            orderGid,
            fulfillmentOrderIds,
            jeevesCheck.TrackingUrl,
            jeevesCheck.TrackingNumber,
            cancellationToken);
        if (!createResult.Success)
        {
            return FailedRow(
                orderId,
                orderGid,
                "SHP-CMP-012",
                createResult.ErrorMessage ?? "Shopify fulfillmentCreate failed",
                "Resolve fulfillment user errors and retry",
                false,
                jeevesCheck.JeevesOrderStatus,
                fulfillmentOrderIds);
        }

        try
        {
            await _shopifyFulfillmentService.TryAddTagAsync(endpointUrl, accessToken, orderGid, ShippedTag, cancellationToken);
        }
        catch
        {
            // Tagging is informational only.
        }

        var closeApplied = false;
        if (closeOrder)
        {
            var closeResult = await _shopifyFulfillmentService.CloseOrderAsync(endpointUrl, accessToken, orderGid, cancellationToken);
            if (!closeResult.Success)
            {
                return FailedRow(
                    orderId,
                    orderGid,
                    "SHP-CMP-012",
                    closeResult.ErrorMessage ?? "Shopify orderClose failed",
                    "Resolve close-order user errors or rerun without close-order",
                    false,
                    jeevesCheck.JeevesOrderStatus,
                    fulfillmentOrderIds,
                    createResult.FulfillmentId);
            }

            closeApplied = closeResult.CloseApplied;
        }

        return new FlowEngineShopifyCompleteOrderRow
        {
            OrderId = orderId,
            OrderGid = orderGid,
            Status = "completed",
            Validation = EligibleDecision(),
            FulfillmentOrderIds = fulfillmentOrderIds,
            FulfillmentId = createResult.FulfillmentId,
            CloseApplied = closeApplied,
            JeevesOrderStatus = jeevesCheck.JeevesOrderStatus
        };
    }

    private async Task<List<ShopifyOrderNode>> CollectOrdersAsync(
        Uri endpointUrl,
        string accessToken,
        string searchQuery,
        int limit,
        CancellationToken cancellationToken)
    {
        var collected = new List<ShopifyOrderNode>();
        string? cursor = null;

        while (collected.Count < limit)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fetchCount = Math.Min(DefaultPageSize, Math.Max(1, limit - collected.Count));
            if (limit == int.MaxValue)
                fetchCount = DefaultPageSize;

            var response = await PostGraphQlAsync<ShopifyFetchOrdersData>(
                endpointUrl,
                accessToken,
                _shopifyQueryCatalog.FetchOrdersByDateQuery,
                new Dictionary<string, object?>
                {
                    ["first"] = fetchCount,
                    ["after"] = cursor,
                    ["query"] = searchQuery
                },
                "ShopifyFetchOrdersByDate",
                cancellationToken);

            var edges = response.Orders?.Edges ?? new List<ShopifyOrderEdge>();
            if (edges.Count == 0)
                break;

            collected.AddRange(edges.Where(edge => edge.Node is not null).Select(edge => edge.Node!));

            var pageInfo = response.Orders?.PageInfo;
            if (pageInfo?.HasNextPage != true || string.IsNullOrWhiteSpace(pageInfo.EndCursor))
                break;

            cursor = pageInfo.EndCursor;
        }

        return limit == int.MaxValue ? collected : collected.Take(limit).ToList();
    }

    private async Task<ShopifyOrderNode?> FetchOrderAsync(
        Uri endpointUrl,
        string accessToken,
        string orderGid,
        CancellationToken cancellationToken)
    {
        var response = await PostGraphQlAsync<ShopifyFetchOrderData>(
            endpointUrl,
            accessToken,
            _shopifyQueryCatalog.FetchOrderQuery,
            new Dictionary<string, object?>
            {
                ["id"] = orderGid
            },
            "ShopifyFetchOrder",
            cancellationToken);

        return response.Order;
    }

    private async Task<JeevesCheckResult> CheckJeevesOrderAsync(
        JeevesRuntimeContext runtimeContext,
        IntegrationSourceConfig jeevesConfig,
        string extOrderNr,
        CancellationToken cancellationToken)
    {
        var token = await GetJeevesAccessTokenAsync(runtimeContext.CompanyId, jeevesConfig, cancellationToken);
        var uri = BuildRequestUri(
            jeevesConfig.BaseUrl!,
            "orders",
            new Dictionary<string, string?>
            {
                ["c_foretagkod"] = runtimeContext.CompanyCode.ToString(CultureInfo.InvariantCulture),
                ["c_extordernr"] = extOrderNr
            });

        var response = await SendJeevesAsync(token, uri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _jeevesAuthService.Invalidate($"{runtimeContext.CompanyId}:jeeves");
            token = await GetJeevesAccessTokenAsync(runtimeContext.CompanyId, jeevesConfig, cancellationToken);
            response = await SendJeevesAsync(token, uri, cancellationToken);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new JeevesCheckResult(JeevesLookupStatus.NotFound, 0, null, null, null);

        if (!response.IsSuccessStatusCode)
            return new JeevesCheckResult(JeevesLookupStatus.Error, 0, null, null, $"Jeeves svarade med {(int)response.StatusCode}: {TrimForError(response.Body)}");

        var rows = ParseLookupRows(response.Body);
        var first = rows.FirstOrDefault();
        if (first is null)
            return new JeevesCheckResult(JeevesLookupStatus.NotFound, 0, null, null, null);

        var trackingUrl = ExtractString(first, "egetAttribut3", "egetattribut3", "c_egetattribut3", "c_egetAttribut3", "trackingUrl", "trackingURL");
        return new JeevesCheckResult(
            JeevesLookupStatus.Found,
            ExtractInt(first, "c_ordstat", "ordstat", "ordStat", "orderStatus") ?? 0,
            trackingUrl,
            ExtractTrackingNumber(trackingUrl),
            null);
    }

    private async Task<string> GetJeevesAccessTokenAsync(Guid companyId, IntegrationSourceConfig config, CancellationToken cancellationToken)
    {
        var token = await _jeevesAuthService.GetAccessTokenAsync(
            $"{companyId}:jeeves",
            config.AuthUrl!,
            config.AppId!,
            config.AppSecret!,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Kunde inte hamta access token for Jeeves.");

        return token;
    }

    private async Task<AuthorizedResponse> SendJeevesAsync(string token, string uri, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("Integration.Jeeves");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new AuthorizedResponse(response.StatusCode, body);
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

    private IntegrationSourceConfig ResolveConfig(Guid companyId, IntegrationSource source)
    {
        var company = _integrationOptions.Value.Companies.FirstOrDefault(entry => entry.CompanyId == companyId);
        var config = company?.GetSource(source);
        if (config is null || string.IsNullOrWhiteSpace(config.BaseUrl))
            throw new InvalidOperationException($"{source} integration saknar BaseUrl for FlowEngine.");

        if (source == IntegrationSource.Jeeves &&
            (string.IsNullOrWhiteSpace(config.AuthUrl) ||
             string.IsNullOrWhiteSpace(config.AppId) ||
             string.IsNullOrWhiteSpace(config.AppSecret)))
        {
            throw new InvalidOperationException("Jeeves integration maste ha BaseUrl, AuthUrl, AppId och AppSecret for Shopify complete orders pending.");
        }

        if (source == IntegrationSource.Shopify &&
            string.IsNullOrWhiteSpace(config.Token) &&
            (string.IsNullOrWhiteSpace(config.AppId) || string.IsNullOrWhiteSpace(config.AppSecret)))
        {
            throw new InvalidOperationException("Shopify integration maste ha Token eller AppId/AppSecret for FlowEngine.");
        }

        return config;
    }

    private static string BuildPendingCompletionSearchQuery()
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-PendingLookbackDays).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        return $"status:any tag:{JeevesSyncedTag} -tag:{ShippedTag} created_at:>={cutoff}";
    }

    private string? ResolveNumericId(ShopifyOrderNode order)
    {
        if (!string.IsNullOrWhiteSpace(order.LegacyResourceId))
            return order.LegacyResourceId.Trim();

        return _shopifySelectionService.ExtractNumericIdFromGid(order.Id);
    }

    private FlowEngineShopifyOrderReference BuildReference(ShopifyOrderNode order)
        => new(order.Id, ResolveNumericId(order));

    private static string NormalizeState(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static string? ExtractTrackingNumber(string? trackingUrl)
    {
        if (string.IsNullOrWhiteSpace(trackingUrl))
            return null;

        var value = trackingUrl.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            value = uri.AbsolutePath;

        var segment = value.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (string.IsNullOrWhiteSpace(segment))
            return null;

        segment = segment.Split('?', '#')[0].Trim();
        return string.IsNullOrWhiteSpace(segment) ? null : Uri.UnescapeDataString(segment);
    }

    private static FlowEngineShopifyCompleteOrderRow FailedRow(
        string orderId,
        string? orderGid,
        string ruleId,
        string message,
        string remediation,
        bool validationFailure = false,
        int? jeevesOrderStatus = null,
        IReadOnlyList<string>? fulfillmentOrderIds = null,
        string? fulfillmentId = null)
        => new()
        {
            OrderId = orderId,
            OrderGid = orderGid,
            Status = "failed",
            Validation = new FlowEngineShopifyValidationDecision
            {
                Status = "failed",
                RuleId = ruleId,
                Classification = "failed",
                Message = message,
                Remediation = remediation
            },
            FulfillmentOrderIds = fulfillmentOrderIds?.ToList() ?? new List<string>(),
            FulfillmentId = fulfillmentId,
            JeevesOrderStatus = jeevesOrderStatus,
            ErrorMessage = message
        };

    private static FlowEngineShopifyCompleteOrderRow SkippedRow(
        string orderId,
        string? orderGid,
        string status,
        string ruleId,
        string message,
        string remediation,
        int? jeevesOrderStatus = null)
        => new()
        {
            OrderId = orderId,
            OrderGid = orderGid,
            Status = status,
            Validation = new FlowEngineShopifyValidationDecision
            {
                Status = "skipped",
                RuleId = ruleId,
                Classification = "skipped",
                Message = message,
                Remediation = remediation
            },
            JeevesOrderStatus = jeevesOrderStatus,
            ErrorMessage = message
        };

    private static FlowEngineShopifyValidationDecision EligibleDecision()
        => new()
        {
            Status = "eligible"
        };

    private static IReadOnlyList<Dictionary<string, JsonElement>> ParseLookupRows(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return Array.Empty<Dictionary<string, JsonElement>>();

        try
        {
            using var document = JsonDocument.Parse(body);
            return ParseRows(document.RootElement);
        }
        catch (JsonException)
        {
            return Array.Empty<Dictionary<string, JsonElement>>();
        }
    }

    private static IReadOnlyList<Dictionary<string, JsonElement>> ParseRows(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object).Select(ToDictionary).ToList();

        if (root.ValueKind != JsonValueKind.Object)
            return Array.Empty<Dictionary<string, JsonElement>>();

        foreach (var key in new[] { "orders", "Orders", "items", "Items", "data", "Data" })
        {
            if (root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array)
                return value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object).Select(ToDictionary).ToList();
        }

        return new[] { ToDictionary(root) };
    }

    private static Dictionary<string, JsonElement> ToDictionary(JsonElement element)
    {
        var dictionary = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
            dictionary[property.Name] = property.Value.Clone();
        return dictionary;
    }

    private static int? ExtractInt(Dictionary<string, JsonElement> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!row.TryGetValue(key, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
                return numeric;
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric))
                return numeric;
        }

        return null;
    }

    private static string? ExtractString(Dictionary<string, JsonElement> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!row.TryGetValue(key, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.String)
                return value.GetString();
            if (value.ValueKind == JsonValueKind.Number)
                return value.GetRawText();
        }

        return null;
    }

    private static string BuildRequestUri(string baseUrl, string relativePath, IReadOnlyDictionary<string, string?> query)
    {
        var baseUri = new Uri(baseUrl.TrimEnd('/') + "/");
        var builder = new UriBuilder(new Uri(baseUri, relativePath.TrimStart('/')));
        builder.Query = string.Join(
            "&",
            query.Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
                .Select(entry => $"{Uri.EscapeDataString(entry.Key)}={Uri.EscapeDataString(entry.Value!)}"));
        return builder.Uri.ToString();
    }

    private static string TrimForError(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Empty response body";

        var trimmed = value.Trim();
        return trimmed.Length <= 320 ? trimmed : trimmed[..320];
    }

    private sealed record AuthorizedResponse(HttpStatusCode StatusCode, string Body)
    {
        public bool IsSuccessStatusCode => (int)StatusCode is >= 200 and <= 299;
    }

    private sealed record JeevesCheckResult(JeevesLookupStatus Status, int JeevesOrderStatus, string? TrackingUrl, string? TrackingNumber, string? ErrorMessage);

    private enum JeevesLookupStatus
    {
        Found,
        NotFound,
        Error
    }

    private sealed class ShopifyFetchOrdersData
    {
        public ShopifyOrdersConnection? Orders { get; set; }
    }

    private sealed class ShopifyFetchOrderData
    {
        public ShopifyOrderNode? Order { get; set; }
    }

    private sealed class ShopifyOrdersConnection
    {
        public ShopifyPageInfo? PageInfo { get; set; }
        public List<ShopifyOrderEdge>? Edges { get; set; }
    }

    private sealed class ShopifyOrderEdge
    {
        public ShopifyOrderNode? Node { get; set; }
    }

    private sealed class ShopifyOrderNode
    {
        public string? Id { get; set; }
        public string? LegacyResourceId { get; set; }
        public string? Name { get; set; }
        public string? CreatedAt { get; set; }
        public string? UpdatedAt { get; set; }
        public string? CancelledAt { get; set; }
        public bool? Test { get; set; }
        public string? DisplayFinancialStatus { get; set; }
        public string? DisplayFulfillmentStatus { get; set; }
    }

    private sealed class ShopifyPageInfo
    {
        public bool HasNextPage { get; set; }
        public string? EndCursor { get; set; }
    }
}

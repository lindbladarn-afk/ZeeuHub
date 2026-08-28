using System.Text.Json;
using System.Text.Json.Serialization;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineShopifyReadResultFactory : IFlowEngineShopifyReadResultFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public FlowEngineOperationExecutionData BuildScopesCheckExecution(
        FlowEngineShopifyScopeProbeResult scopeProbe,
        IReadOnlyList<FlowEngineShopifyScopeProbeCategory> categories,
        string storeDomain)
    {
        var payload = new
        {
            storeName = scopeProbe.ShopName,
            storeDomain = scopeProbe.ShopDomain ?? storeDomain,
            apiVersion = "2025-01",
            grantedScopes = scopeProbe.Scopes.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            backfillLimited = !scopeProbe.Scopes.Contains("read_all_orders"),
            categories = categories.Select(category => new
            {
                category = category.Category,
                status = category.IsSatisfied ? "PASS" : "FAIL",
                missingRequiredScopes = category.MissingRequiredScopes,
                missingAnyOfScopes = category.MissingAnyOfScopes
            }).ToArray()
        };
        var missingCategoryCount = payload.categories.Count(category => category.status == "FAIL");

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Shopify scopes-check: Store={payload.storeDomain}, Granted={payload.grantedScopes.Length}, MissingCategories={missingCategoryCount}",
                payload.backfillLimited
                    ? "Backfill: LIMITED (read_all_orders saknas)"
                    : "Backfill: PASS"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

    public FlowEngineOperationExecutionData BuildGetProductsExecution(
        string companyName,
        int companyCode,
        string storeDomain,
        int effectiveLimit,
        string? updatedSince,
        string? searchQuery,
        bool includeInventoryItem,
        bool includeMetafields,
        FlowEngineShopifyCollectProductsResult pageResult,
        IReadOnlyList<ShopifyProductNode> orderedProducts)
    {
        var payload = new
        {
            query = searchQuery,
            updatedSince,
            limit = effectiveLimit,
            hasNextPage = pageResult.HasNextPage,
            endCursor = pageResult.EndCursor,
            counts = new
            {
                fetched = orderedProducts.Count,
                total = orderedProducts.Count
            },
            products = orderedProducts
        };

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Shopify get-products: Fetched={orderedProducts.Count}, Limit={effectiveLimit}, Store={storeDomain}",
                $"Portalbolag: {companyName} ({companyCode})",
                includeInventoryItem ? "Inventory fields: included" : "Inventory fields: omitted (read_inventory saknas)",
                includeMetafields ? "Metafields: included" : "Metafields: omitted (read_metafields saknas)"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

    public FlowEngineOperationExecutionData BuildFetchOrderExecution(
        string numericId,
        string orderGid,
        ShopifyOrderDetailNode order,
        string storeDomain)
    {
        var payload = new
        {
            orderId = numericId,
            orderGid,
            order
        };

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Shopify fetch-order {numericId}: Success",
                $"Shopify store: {storeDomain}"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

    public FlowEngineOperationExecutionData BuildFetchOrdersExecution(
        string? date,
        string sinceUtc,
        string untilUtc,
        bool useLatestDay,
        string selectionKind,
        IReadOnlyList<object> days,
        int totalOrders,
        string storeDomain,
        string selectionSummaryLabel)
    {
        var payload = new
        {
            date,
            sinceUtc,
            untilUtc,
            useLatestDay,
            selectionKind,
            days
        };

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Shopify fetch-orders {selectionSummaryLabel}: Total={totalOrders}",
                $"Shopify store: {storeDomain}"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

    public FlowEngineOperationExecutionData BuildValidateOrderExecution(
        string orderId,
        string? orderGid,
        FlowEngineShopifyValidationDecision validation,
        string storeDomain)
    {
        var payload = new FlowEngineShopifyValidateOrderPayload
        {
            OrderId = orderId,
            OrderGid = orderGid,
            Validation = validation
        };

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Shopify validate-order {payload.OrderId}: Status={payload.Validation.Status}",
                $"Shopify store: {storeDomain}"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

    public FlowEngineShopifyValidateOrdersPayload CreateValidateOrdersPayload(
        string? date,
        string sinceUtc,
        string untilUtc,
        bool useLatestDay,
        string selectionKind)
        => new()
        {
            Date = date,
            SinceUtc = sinceUtc,
            UntilUtc = untilUtc,
            UseLatestDay = useLatestDay,
            SelectionKind = selectionKind
        };

    public FlowEngineShopifyValidateOrdersDayPayload CreateValidateOrdersDay(string date)
        => new() { Date = date };

    public FlowEngineOperationExecutionData BuildValidateOrdersExecution(
        FlowEngineShopifyValidateOrdersPayload payload,
        string selectionSummaryLabel,
        string storeDomain)
    {
        var total = payload.Days.Sum(day => day.Counts.Total);
        var eligible = payload.Days.Sum(day => day.Counts.Eligible);
        var skipped = payload.Days.Sum(day => day.Counts.Skipped);
        var failed = payload.Days.Sum(day => day.Counts.Failed);

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Shopify validate-orders {selectionSummaryLabel}: Total={total}, Eligible={eligible}, Skipped={skipped}, Failed={failed}",
                $"Shopify store: {storeDomain}"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

    public FlowEngineShopifyCheckOrdersPayload CreateCheckOrdersPayload(
        string? date,
        string sinceUtc,
        string untilUtc,
        bool useLatestDay,
        string selectionKind)
        => new()
        {
            Date = date,
            SinceUtc = sinceUtc,
            UntilUtc = untilUtc,
            UseLatestDay = useLatestDay,
            SelectionKind = selectionKind
        };

    public FlowEngineShopifyCheckOrdersDayPayload CreateCheckOrdersDay(string date)
        => new() { Date = date };

    public FlowEngineOperationExecutionData BuildCheckOrdersExecution(
        FlowEngineShopifyCheckOrdersPayload payload,
        string selectionSummaryLabel,
        string storeDomain)
    {
        var total = payload.Days.Sum(day => day.Counts.Total);
        var found = payload.Days.Sum(day => day.Counts.Found);
        var missing = payload.Days.Sum(day => day.Counts.Missing);
        var failedValidation = payload.Days.Sum(day => day.Counts.FailedValidation);
        var error = payload.Days.Sum(day => day.Counts.Error);

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Shopify check-orders {selectionSummaryLabel}: Total={total}, Found={found}, Missing={missing}, FailedValidation={failedValidation}, Error={error}",
                $"Shopify store: {storeDomain}"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

    public FlowEngineOperationExecutionData BuildSendOrderExecution(
        string storeDomain,
        bool dryRun,
        bool skipJeevesCheck,
        string orderId,
        string? orderGid,
        string status,
        FlowEngineShopifyValidationDecision? validation,
        FlowEngineShopifyJeevesOrderPayload? mappedPayload,
        string? errorMessage)
    {
        var payload = new FlowEngineShopifySendOrderPayload
        {
            OrderId = orderId,
            OrderGid = orderGid,
            Status = status,
            Validation = validation ?? FailedDecision("SHP-VAL-012", errorMessage ?? "Send failed", "Resolve mapping and rerun"),
            Payload = mappedPayload,
            ErrorMessage = errorMessage
        };

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Shopify send-order {payload.OrderId}: Status={payload.Status}",
                $"Mode: {(dryRun ? "dry run" : "send to Jeeves")} | Skip Jeeves check: {(skipJeevesCheck ? "yes" : "no")}",
                $"Shopify store: {storeDomain}"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

    public FlowEngineShopifySendOrdersPayload CreateSendOrdersPayload(
        string? date,
        string sinceUtc,
        string untilUtc,
        bool useLatestDay,
        string selectionKind,
        bool dryRun,
        bool skipJeevesCheck)
        => new()
        {
            Date = date,
            SinceUtc = sinceUtc,
            UntilUtc = untilUtc,
            UseLatestDay = useLatestDay,
            SelectionKind = selectionKind,
            DryRun = dryRun,
            SkipJeevesCheck = skipJeevesCheck
        };

    public FlowEngineShopifySendOrdersDayPayload CreateSendOrdersDay(string date)
        => new() { Date = date };

    public void AddSendOrderOutcome(FlowEngineShopifySendOrdersDayPayload day, string orderId, string? orderNumber, string? orderGid, string status, FlowEngineShopifyValidationDecision? validation, string? errorMessage)
    {
        IncrementSendCounts(day.Counts, status);
        if (string.Equals(status, "mapped", StringComparison.Ordinal) || string.Equals(status, "sent", StringComparison.Ordinal))
            return;

        day.Orders.Add(new FlowEngineShopifySendOrderRow
        {
            OrderId = orderId,
            OrderNumber = orderNumber,
            OrderGid = orderGid,
            Status = status,
            Validation = validation,
            ErrorMessage = errorMessage
        });
    }

    public FlowEngineOperationExecutionData BuildSendOrdersExecution(
        FlowEngineShopifySendOrdersPayload payload,
        string selectionSummaryLabel,
        string storeDomain)
    {
        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Shopify send-orders {selectionSummaryLabel}: Total={payload.Counts.Total}, Mapped={payload.Counts.Mapped}, Sent={payload.Counts.Sent}, SkippedExisting={payload.Counts.SkippedExisting}, SkippedIneligible={payload.Counts.SkippedIneligible}, Failed={payload.Counts.Failed}",
                $"Mode: {(payload.DryRun ? "dry run" : "send to Jeeves")} | Skip Jeeves check: {(payload.SkipJeevesCheck ? "yes" : "no")}",
                $"Shopify store: {storeDomain}"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

    private static FlowEngineShopifyValidationDecision FailedDecision(string ruleId, string message, string remediation)
        => new()
        {
            Status = "failed",
            RuleId = ruleId,
            Classification = "failed",
            Message = message,
            Remediation = remediation
        };

    private static void IncrementSendCounts(FlowEngineShopifySendCounts counts, string status)
    {
        counts.Total++;
        switch (status)
        {
            case "mapped":
                counts.Mapped++;
                break;
            case "sent":
                counts.Sent++;
                break;
            case "skipped_existing":
                counts.SkippedExisting++;
                break;
            case "skipped_ineligible":
                counts.SkippedIneligible++;
                break;
            case "failed":
                counts.Failed++;
                break;
        }
    }
}

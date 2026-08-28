using WebApp.Models.Integration;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCommandLineBuilder : IFlowEngineCommandLineBuilder
{
    public IReadOnlyList<string> BuildArguments(FlowEngineExecuteJobRequest request, JeevesRuntimeContext runtimeContext)
    {
        var companyCode = request.Params.JeevesCompanyCode ?? runtimeContext.CompanyCode;

        var arguments = request.Operation switch
        {
            FlowEngineOperationType.ConfigValidate => new[] { "config", "validate" },
            FlowEngineOperationType.CentraFetchOrder => BuildCentraFetchOrderArguments(request.Params.OrderId).ToArray(),
            FlowEngineOperationType.CentraFetchOrders => BuildCentraDateRangeArguments(
                "fetch-orders",
                request.Params.DateUtc,
                request.Params.SinceUtc,
                request.Params.UntilUtc,
                request.Params.UseLatestDay,
                request.Flags.ForceRange).ToArray(),
            FlowEngineOperationType.CentraFetchReturn => BuildCentraFetchReturnArguments(request.Params.ReturnId).ToArray(),
            FlowEngineOperationType.CentraFetchReturns => BuildCentraDateRangeArguments(
                "fetch-returns",
                request.Params.DateUtc,
                request.Params.SinceUtc,
                request.Params.UntilUtc,
                request.Params.UseLatestDay,
                request.Flags.ForceRange).ToArray(),
            FlowEngineOperationType.CheckOrders => BuildCheckOrdersArguments(request.Params.DateUtc, request.Params.UseLimit ? request.Params.Limit : null).ToArray(),
            FlowEngineOperationType.CreateShipments => BuildCreateShipmentsArguments(
                request.Params.DateUtc,
                request.Params.UseLimit ? request.Params.Limit : null,
                request.Flags.DryRun).ToArray(),
            FlowEngineOperationType.CreateShipment => BuildCreateShipmentArguments(
                request.Params.OrderId,
                request.Flags.DryRun).ToArray(),
            FlowEngineOperationType.CreateShipmentsPending => BuildCreateShipmentsPendingArguments(
                request.Params.UseLimit ? request.Params.Limit : null,
                request.Flags.DryRun).ToArray(),
            FlowEngineOperationType.SendOrder => BuildSendOrderArguments(
                request.Params.OrderId,
                request.Flags.DryRun,
                request.Flags.SkipJeevesCheck).ToArray(),
            FlowEngineOperationType.SendOrders => BuildSendOrdersArguments(
                request.Params.DateUtc,
                request.Params.UseLimit ? request.Params.Limit : null,
                request.Flags.DryRun,
                request.Flags.SkipJeevesCheck).ToArray(),
            FlowEngineOperationType.SendReturn => BuildSendReturnArguments(
                request.Params.ReturnId,
                request.Flags.DryRun).ToArray(),
            FlowEngineOperationType.SendReturns => BuildSendReturnsArguments(
                request.Params.DateUtc,
                request.Params.UseLimit ? request.Params.Limit : null,
                request.Flags.DryRun).ToArray(),
            FlowEngineOperationType.CompleteOrder => BuildCompleteOrderArguments(
                request.Params.OrderId,
                request.Flags.DryRun,
                request.Flags.CloseOrder).ToArray(),
            FlowEngineOperationType.AkeneoProducts => BuildAkeneoProductsArguments(
                request.Params.AkeneoSkus,
                request.Params.UseLimit ? request.Params.Limit : null).ToArray(),
            FlowEngineOperationType.AkeneoAllProducts => BuildAkeneoAllProductsArguments(
                request.Params.UseLimit ? request.Params.Limit : null).ToArray(),
            FlowEngineOperationType.AkeneoSendToShopify => BuildAkeneoSendToShopifyArguments(
                request.Params.AkeneoSkus,
                request.Params.UseLimit ? request.Params.Limit : null,
                request.Flags.DryRun).ToArray(),
            FlowEngineOperationType.AkeneoSendToCentra => BuildAkeneoSendToCentraArguments(
                request.Params.AkeneoSkus,
                request.Params.UseLimit ? request.Params.Limit : null,
                request.Flags.DryRun).ToArray(),
            FlowEngineOperationType.ShopifyScopesCheck => new[] { "shopify", "scopes-check" },
            FlowEngineOperationType.ShopifyGetProducts => BuildShopifyGetProductsArguments(
                request.Params.ShopifyQuery,
                request.Params.ShopifyUpdatedSince,
                request.Params.UseLimit ? request.Params.Limit : null).ToArray(),
            FlowEngineOperationType.ShopifyFetchOrder => BuildShopifyFetchOrderArguments(request.Params.OrderId).ToArray(),
            FlowEngineOperationType.ShopifyFetchOrders => BuildShopifyDateRangeArguments(
                "fetch-orders",
                request.Params.DateUtc,
                request.Params.SinceUtc,
                request.Params.UntilUtc,
                request.Params.UseLatestDay,
                request.Params.UseLimit ? request.Params.Limit : null,
                request.Flags.ForceRange).ToArray(),
            FlowEngineOperationType.ShopifyValidateOrder => BuildShopifyValidateOrderArguments(request.Params.OrderId).ToArray(),
            FlowEngineOperationType.ShopifyValidateOrders => BuildShopifyDateRangeArguments(
                "validate-orders",
                request.Params.DateUtc,
                request.Params.SinceUtc,
                request.Params.UntilUtc,
                request.Params.UseLatestDay,
                request.Params.UseLimit ? request.Params.Limit : null,
                request.Flags.ForceRange).ToArray(),
            FlowEngineOperationType.ShopifyCheckOrders => BuildShopifyDateRangeArguments(
                "check-orders",
                request.Params.DateUtc,
                request.Params.SinceUtc,
                request.Params.UntilUtc,
                request.Params.UseLatestDay,
                request.Params.UseLimit ? request.Params.Limit : null,
                request.Flags.ForceRange).ToArray(),
            FlowEngineOperationType.ShopifySendOrder => BuildShopifySendOrderArguments(
                request.Params.OrderId,
                request.Flags.DryRun,
                request.Flags.SkipJeevesCheck).ToArray(),
            FlowEngineOperationType.ShopifySendOrders => BuildShopifySendOrdersArguments(
                request.Params.DateUtc,
                request.Params.SinceUtc,
                request.Params.UntilUtc,
                request.Params.UseLatestDay,
                request.Params.UseLimit ? request.Params.Limit : null,
                request.Flags.DryRun,
                request.Flags.SkipJeevesCheck,
                request.Flags.ForceRange).ToArray(),
            FlowEngineOperationType.CompleteOrders => BuildCompleteOrdersArguments(
                request.Params.DateUtc,
                request.Params.SinceUtc,
                request.Params.UntilUtc,
                request.Params.UseLatestDay,
                request.Params.UseLimit ? request.Params.Limit : null,
                request.Flags.DryRun,
                request.Flags.CloseOrder,
                request.Flags.ForceRange).ToArray(),
            FlowEngineOperationType.CompleteOrdersPending => BuildCompleteOrdersPendingArguments(
                request.Params.UseLimit ? request.Params.Limit : null,
                request.Flags.DryRun,
                request.Flags.CloseOrder).ToArray(),
            FlowEngineOperationType.GetOrders => BuildJeevesGetOrdersArguments(
                companyCode,
                request.Params.JeevesLookupField,
                request.Params.JeevesLookupValue).ToArray(),
            FlowEngineOperationType.OrderExists => BuildJeevesOrderExistsArguments(request.Params.OrderId).ToArray(),
            FlowEngineOperationType.GetCustomerAddresses => new[]
            {
                "jeeves",
                "get-customer-addresses",
                "--c-foretagkod",
                companyCode.ToString(),
                "--c-ftgnr",
                request.Params.JeevesCustomerNumber?.Trim() ?? string.Empty
            },
            FlowEngineOperationType.GetProduct => new[]
            {
                "jeeves",
                "get-product",
                "--c-foretagkod",
                companyCode.ToString(),
                "--c-artnr",
                request.Params.JeevesProductArticleNumber?.Trim() ?? string.Empty
            },
            FlowEngineOperationType.GetArtStatus => BuildArtStatusArguments(companyCode, request.Params.JeevesProductArticleNumbers).ToArray(),
            FlowEngineOperationType.ImportOrder => BuildImportOrderArguments(companyCode, request.Params.JeevesImportOrder, request.Flags.DryRun).ToArray(),
            _ => new[] { request.Operation.ToString() }
        };

        return AppendGlobalWorkbenchFlags(arguments, request.Flags);
    }

    private static IReadOnlyList<string> AppendGlobalWorkbenchFlags(IReadOnlyList<string> arguments, FlowEngineExecutionFlags flags)
    {
        var result = arguments.ToList();

        if (flags.TestMode && !result.Contains("--test", StringComparer.Ordinal))
            result.Add("--test");

        return result;
    }

    private static IEnumerable<string> BuildCheckOrdersArguments(string? dateUtc, int? limit)
    {
        var date = string.IsNullOrWhiteSpace(dateUtc)
            ? DateTime.UtcNow.ToString("yyyy-MM-dd")
            : dateUtc.Trim();

        var arguments = new List<string>
        {
            "centra",
            "check-orders",
            "--date",
            date
        };

        if (limit.HasValue && limit.Value > 0)
        {
            arguments.Add("--limit");
            arguments.Add(limit.Value.ToString());
        }

        return arguments;
    }

    private static IEnumerable<string> BuildSendOrdersArguments(string? dateUtc, int? limit, bool dryRun, bool skipJeevesCheck)
    {
        var date = string.IsNullOrWhiteSpace(dateUtc)
            ? DateTime.UtcNow.ToString("yyyy-MM-dd")
            : dateUtc.Trim();

        var arguments = new List<string>
        {
            "centra",
            "send-orders",
            "--date",
            date
        };

        if (limit.HasValue && limit.Value > 0)
        {
            arguments.Add("--limit");
            arguments.Add(limit.Value.ToString());
        }

        if (dryRun)
            arguments.Add("--dry-run");

        if (skipJeevesCheck)
            arguments.Add("--skip-jeeves-check");

        return arguments;
    }

    private static IEnumerable<string> BuildSendOrderArguments(string? orderId, bool dryRun, bool skipJeevesCheck)
    {
        var arguments = new List<string>
        {
            "centra",
            "send-order",
            "--order-id",
            string.IsNullOrWhiteSpace(orderId) ? string.Empty : orderId.Trim()
        };

        if (dryRun)
            arguments.Add("--dry-run");

        if (skipJeevesCheck)
            arguments.Add("--skip-jeeves-check");

        return arguments;
    }

    private static IEnumerable<string> BuildCreateShipmentsArguments(string? dateUtc, int? limit, bool dryRun)
    {
        var date = string.IsNullOrWhiteSpace(dateUtc)
            ? DateTime.UtcNow.ToString("yyyy-MM-dd")
            : dateUtc.Trim();

        var arguments = new List<string>
        {
            "centra",
            "create-shipments",
            "--date",
            date
        };

        if (limit.HasValue && limit.Value > 0)
        {
            arguments.Add("--limit");
            arguments.Add(limit.Value.ToString());
        }

        if (dryRun)
            arguments.Add("--dry-run");

        return arguments;
    }

    private static IEnumerable<string> BuildCreateShipmentsPendingArguments(int? limit, bool dryRun)
    {
        var arguments = new List<string>
        {
            "centra",
            "create-shipments-pending"
        };

        if (limit.HasValue && limit.Value > 0)
        {
            arguments.Add("--limit");
            arguments.Add(limit.Value.ToString());
        }

        if (dryRun)
            arguments.Add("--dry-run");

        return arguments;
    }

    private static IEnumerable<string> BuildCreateShipmentArguments(string? orderId, bool dryRun)
    {
        var arguments = new List<string>
        {
            "centra",
            "create-shipments",
            string.IsNullOrWhiteSpace(orderId) ? string.Empty : orderId.Trim()
        };

        if (dryRun)
            arguments.Add("--dry-run");

        return arguments;
    }

    private static IEnumerable<string> BuildSendReturnsArguments(string? dateUtc, int? limit, bool dryRun)
    {
        var date = string.IsNullOrWhiteSpace(dateUtc)
            ? DateTime.UtcNow.ToString("yyyy-MM-dd")
            : dateUtc.Trim();

        var arguments = new List<string>
        {
            "centra",
            "send-returns",
            "--date",
            date
        };

        if (limit.HasValue && limit.Value > 0)
        {
            arguments.Add("--limit");
            arguments.Add(limit.Value.ToString());
        }

        if (dryRun)
            arguments.Add("--dry-run");

        return arguments;
    }

    private static IEnumerable<string> BuildSendReturnArguments(string? returnId, bool dryRun)
    {
        var arguments = new List<string>
        {
            "centra",
            "send-return",
            "--return-id",
            string.IsNullOrWhiteSpace(returnId) ? string.Empty : returnId.Trim()
        };

        if (dryRun)
            arguments.Add("--dry-run");

        return arguments;
    }

    private static IEnumerable<string> BuildCompleteOrdersPendingArguments(int? limit, bool dryRun, bool closeOrder)
    {
        var arguments = new List<string>
        {
            "shopify",
            "complete-orders-pending"
        };

        if (limit.HasValue && limit.Value > 0)
        {
            arguments.Add("--limit");
            arguments.Add(limit.Value.ToString());
        }

        if (dryRun)
            arguments.Add("--dry-run");

        if (closeOrder)
            arguments.Add("--close-order");

        return arguments;
    }

    private static IEnumerable<string> BuildCompleteOrdersArguments(string? dateUtc, string? sinceUtc, string? untilUtc, bool useLatestDay, int? limit, bool dryRun, bool closeOrder, bool forceRange)
    {
        var arguments = new List<string>
        {
            "shopify",
            "complete-orders"
        };

        if (useLatestDay)
        {
            arguments.Add("--latest-day");
        }
        else if (!string.IsNullOrWhiteSpace(sinceUtc))
        {
            arguments.Add("--since");
            arguments.Add(sinceUtc.Trim());

            if (!string.IsNullOrWhiteSpace(untilUtc))
            {
                arguments.Add("--until");
                arguments.Add(untilUtc.Trim());
            }
        }
        else
        {
            var date = string.IsNullOrWhiteSpace(dateUtc)
                ? DateTime.UtcNow.ToString("yyyy-MM-dd")
                : dateUtc.Trim();

            arguments.Add("--date");
            arguments.Add(date);
        }

        if (limit.HasValue && limit.Value > 0)
        {
            arguments.Add("--limit");
            arguments.Add(limit.Value.ToString());
        }

        if (dryRun)
            arguments.Add("--dry-run");

        if (closeOrder)
            arguments.Add("--close-order");

        if (forceRange)
            arguments.Add("--force");

        return arguments;
    }

    private static IEnumerable<string> BuildCompleteOrderArguments(string? orderId, bool dryRun, bool closeOrder)
    {
        var arguments = new List<string>
        {
            "shopify",
            "complete-order",
            "--id",
            string.IsNullOrWhiteSpace(orderId) ? string.Empty : orderId.Trim()
        };

        if (dryRun)
            arguments.Add("--dry-run");

        if (closeOrder)
            arguments.Add("--close-order");

        return arguments;
    }

    private static IEnumerable<string> BuildAkeneoProductsArguments(IReadOnlyList<string> skus, int? limit)
    {
        var arguments = new List<string>
        {
            "akeneo",
            "--get-products"
        };

        arguments.AddRange(skus.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));

        if (limit.HasValue && limit.Value > 0)
        {
            arguments.Add("--limit");
            arguments.Add(limit.Value.ToString());
        }

        return arguments;
    }

    private static IEnumerable<string> BuildAkeneoAllProductsArguments(int? limit)
    {
        var arguments = new List<string>
        {
            "akeneo",
            "--get-all-products"
        };

        if (limit.HasValue && limit.Value > 0)
        {
            arguments.Add("--limit");
            arguments.Add(limit.Value.ToString());
        }

        return arguments;
    }

    private static IEnumerable<string> BuildAkeneoSendToShopifyArguments(IReadOnlyList<string> skus, int? limit, bool dryRun)
    {
        var arguments = new List<string>
        {
            "akeneo",
            "send-to-shopify"
        };

        if (dryRun)
            arguments.Add("--dry-run");

        foreach (var sku in skus.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()))
        {
            arguments.Add("--sku");
            arguments.Add(sku);
        }

        if (limit.HasValue && limit.Value > 0)
        {
            arguments.Add("--limit");
            arguments.Add(limit.Value.ToString());
        }

        return arguments;
    }

    private static IEnumerable<string> BuildAkeneoSendToCentraArguments(IReadOnlyList<string> skus, int? limit, bool dryRun)
    {
        var arguments = new List<string>
        {
            "akeneo",
            "send-to-centra"
        };

        if (dryRun)
            arguments.Add("--dry-run");

        foreach (var sku in skus.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()))
        {
            arguments.Add("--sku");
            arguments.Add(sku);
        }

        if (limit.HasValue && limit.Value > 0)
        {
            arguments.Add("--limit");
            arguments.Add(limit.Value.ToString());
        }

        return arguments;
    }

    private static IEnumerable<string> BuildShopifyGetProductsArguments(string? query, string? updatedSince, int? limit)
    {
        var arguments = new List<string>
        {
            "shopify",
            "get-products"
        };

        if (!string.IsNullOrWhiteSpace(query))
        {
            arguments.Add("--query");
            arguments.Add(query.Trim());
        }

        if (!string.IsNullOrWhiteSpace(updatedSince))
        {
            arguments.Add("--updated-since");
            arguments.Add(updatedSince.Trim());
        }

        if (limit.HasValue && limit.Value > 0)
        {
            arguments.Add("--limit");
            arguments.Add(limit.Value.ToString());
        }

        return arguments;
    }

    private static IEnumerable<string> BuildShopifyFetchOrderArguments(string? orderId)
        => new[]
        {
            "shopify",
            "fetch-order",
            "--id",
            string.IsNullOrWhiteSpace(orderId) ? string.Empty : orderId.Trim()
        };

    private static IEnumerable<string> BuildCentraFetchOrderArguments(string? orderId)
        => new[]
        {
            "centra",
            "fetch-order",
            "--order-id",
            string.IsNullOrWhiteSpace(orderId) ? string.Empty : orderId.Trim()
        };

    private static IEnumerable<string> BuildCentraFetchReturnArguments(string? returnId)
        => new[]
        {
            "centra",
            "fetch-return",
            "--return-id",
            string.IsNullOrWhiteSpace(returnId) ? string.Empty : returnId.Trim()
        };

    private static IEnumerable<string> BuildCentraDateRangeArguments(
        string subcommand,
        string? dateUtc,
        string? sinceUtc,
        string? untilUtc,
        bool useLatestDay,
        bool forceRange)
    {
        var arguments = new List<string>
        {
            "centra",
            subcommand
        };

        if (useLatestDay)
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            arguments.Add("--since");
            arguments.Add(today);
            arguments.Add("--until");
            arguments.Add(today);
        }
        else if (!string.IsNullOrWhiteSpace(dateUtc))
        {
            arguments.Add("--since");
            arguments.Add(dateUtc.Trim());
            arguments.Add("--until");
            arguments.Add(dateUtc.Trim());
        }
        else
        {
            arguments.Add("--since");
            arguments.Add(string.IsNullOrWhiteSpace(sinceUtc) ? string.Empty : sinceUtc.Trim());

            if (!string.IsNullOrWhiteSpace(untilUtc))
            {
                arguments.Add("--until");
                arguments.Add(untilUtc.Trim());
            }
        }

        if (forceRange)
            arguments.Add("--force");

        return arguments;
    }

    private static IEnumerable<string> BuildShopifyValidateOrderArguments(string? orderId)
        => new[]
        {
            "shopify",
            "validate-order",
            "--id",
            string.IsNullOrWhiteSpace(orderId) ? string.Empty : orderId.Trim()
        };

    private static IEnumerable<string> BuildShopifySendOrderArguments(string? orderId, bool dryRun, bool skipJeevesCheck)
    {
        var arguments = new List<string>
        {
            "shopify",
            "send-order",
            "--id",
            string.IsNullOrWhiteSpace(orderId) ? string.Empty : orderId.Trim()
        };

        if (dryRun)
            arguments.Add("--dry-run");
        if (skipJeevesCheck)
            arguments.Add("--skip-jeeves-check");

        return arguments;
    }

    private static IEnumerable<string> BuildShopifySendOrdersArguments(
        string? dateUtc,
        string? sinceUtc,
        string? untilUtc,
        bool useLatestDay,
        int? limit,
        bool dryRun,
        bool skipJeevesCheck,
        bool forceRange)
    {
        var arguments = BuildShopifyDateRangeArguments(
            "send-orders",
            dateUtc,
            sinceUtc,
            untilUtc,
            useLatestDay,
            limit,
            forceRange).ToList();

        if (dryRun)
            arguments.Add("--dry-run");
        if (skipJeevesCheck)
            arguments.Add("--skip-jeeves-check");

        return arguments;
    }

    private static IEnumerable<string> BuildShopifyDateRangeArguments(string subcommand, string? dateUtc, string? sinceUtc, string? untilUtc, bool useLatestDay, int? limit, bool forceRange)
    {
        var arguments = new List<string>
        {
            "shopify",
            subcommand
        };

        if (useLatestDay)
        {
            arguments.Add("--latest-day");
        }
        else if (!string.IsNullOrWhiteSpace(sinceUtc))
        {
            arguments.Add("--since");
            arguments.Add(sinceUtc.Trim());

            if (!string.IsNullOrWhiteSpace(untilUtc))
            {
                arguments.Add("--until");
                arguments.Add(untilUtc.Trim());
            }
        }
        else
        {
            var date = string.IsNullOrWhiteSpace(dateUtc)
                ? DateTime.UtcNow.ToString("yyyy-MM-dd")
                : dateUtc.Trim();

            arguments.Add("--date");
            arguments.Add(date);
        }

        if (limit.HasValue && limit.Value > 0)
        {
            arguments.Add("--limit");
            arguments.Add(limit.Value.ToString());
        }

        if (forceRange)
            arguments.Add("--force");

        return arguments;
    }

    private static IEnumerable<string> BuildArtStatusArguments(int companyCode, IReadOnlyList<string> articleNumbers)
    {
        var arguments = new List<string>
        {
            "jeeves",
            "get-art-status",
            "--c-foretagkod",
            companyCode.ToString()
        };

        arguments.AddRange(articleNumbers.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));
        return arguments;
    }

    private static IEnumerable<string> BuildJeevesGetOrdersArguments(int companyCode, string? lookupField, string? lookupValue)
    {
        var normalizedLookupField = string.IsNullOrWhiteSpace(lookupField) ? "c_extordernr" : lookupField.Trim();
        return new[]
        {
            "jeeves",
            "get-orders",
            "--c-foretagkod",
            companyCode.ToString(),
            $"--{normalizedLookupField.Replace("_", "-", StringComparison.Ordinal)}",
            string.IsNullOrWhiteSpace(lookupValue) ? string.Empty : lookupValue.Trim()
        };
    }

    private static IEnumerable<string> BuildJeevesOrderExistsArguments(string? orderId)
        => new[]
        {
            "jeeves",
            "order-exists",
            "--order-id",
            string.IsNullOrWhiteSpace(orderId) ? string.Empty : orderId.Trim()
        };

    private static IEnumerable<string> BuildImportOrderArguments(int companyCode, FlowEngineJeevesImportOrderInput? order, bool dryRun)
    {
        if (order is null)
            return new[] { "jeeves", "import-order" };

        var orderType = order.OrderType.GetValueOrDefault(1);
        var externalOrderNumber = order.ExternalOrderNumber?.Trim() ?? string.Empty;
        var customerReference = string.IsNullOrWhiteSpace(order.CustomerReference)
            ? externalOrderNumber
            : order.CustomerReference.Trim();
        var arguments = new List<string>
        {
            "jeeves",
            "import-order",
            "--c-foretagkod",
            companyCode.ToString(),
            "--c-ftgnr",
            order.CustomerNumber.Trim(),
            "--c-ordtyp",
            orderType.ToString(),
            "--c-kundbestnr",
            customerReference,
            "--c-extordernr",
            externalOrderNumber,
            "--line-count",
            order.Lines.Count.ToString()
        };

        if (!string.IsNullOrWhiteSpace(order.DeliveryPlaceCode))
        {
            arguments.Add("--c-ordlevplats1");
            arguments.Add(order.DeliveryPlaceCode.Trim());
        }

        if (dryRun)
            arguments.Add("--dry-run");

        var requestedCompanyCode = order.CompanyCode.GetValueOrDefault();
        if (requestedCompanyCode > 0 && requestedCompanyCode != companyCode)
        {
            arguments.RemoveRange(2, 2);
            arguments.InsertRange(2, new[] { "--c-foretagkod", requestedCompanyCode.ToString() });
        }

        return arguments;
    }
}

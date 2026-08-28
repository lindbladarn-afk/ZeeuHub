using System.Globalization;
using WebApp.Services.Integration.FlowEngine.CentraSendOrdersContracts;

namespace WebApp.Services.Integration.FlowEngine;

internal static class FlowEngineCentraOrderJeevesMapper
{
    private const int CentraOriginJeevesCompanyCode = 1;
    private const int MaxAddressFieldLength = 72;

    public static JeevesCreateOrderRequest Map(CentraRawOrder order)
    {
        if (string.IsNullOrWhiteSpace(order.Id))
            throw new InvalidOperationException("Order id is required");

        var storeId = order.Store?.Id ?? 0;
        var config = FlowEngineCentraStoreConfigService.GetOrderConfig(storeId);
        var countryCode = NormalizeOptional(order.ShippingAddress?.Country?.Code);
        var ftgNr = FlowEngineCentraStoreConfigService.ResolveOrderCustomerNumber(config, storeId, countryCode, order);
        var grandTotal = order.GrandTotal?.Value ?? 0m;

        return new JeevesCreateOrderRequest
        {
            CompanyCode = CentraOriginJeevesCompanyCode,
            CustomerNumber = ftgNr,
            ExternalOrderNumber = order.Id,
            CustomerReference = FlowEngineCentraOrderMetadataHelper.ResolveCustomerReference(config, order),
            OrderDate = FormatOrderDate(order.CreatedAt),
            OrderType = config.OrderType,
            DeliveryMethodCode = config.UseDeliveryMethodCodeFromAttributes ? FlowEngineCentraOrderMetadataHelper.GetShippingMethodId(order) : null,
            CurrencyCode = order.GrandTotal?.Currency?.Code,
            PaymentCode = null,
            PartialDeliveryAllowed = 0,
            Edit = "FlowEngine",
            DeliveryPlace1 = null,
            DeliveryName = Truncate(FlowEngineCentraCommonHelper.ResolveDeliveryName(config, order), MaxAddressFieldLength),
            DeliveryAddress1 = Truncate(order.ShippingAddress?.Address1, MaxAddressFieldLength),
            DeliveryAddress2 = Truncate(order.ShippingAddress?.Address2, MaxAddressFieldLength),
            DeliveryZipCode = order.ShippingAddress?.ZipCode,
            DeliveryCity = Truncate(order.ShippingAddress?.City, MaxAddressFieldLength),
            DeliveryCountryCode = countryCode,
            CompanyPostNumber = null,
            CustomerReference2 = null,
            GoodsMark1 = null,
            GoodsMark2 = null,
            GoodsMark3 = order.ShippingAddress?.Email,
            GoodsMark4 = order.ShippingAddress?.PhoneNumber,
            EgenParameter1 = storeId == 1 ? NormalizeOptional(order.Market?.Name) : null,
            EgenParameter2 = storeId > 0 ? storeId.ToString(CultureInfo.InvariantCulture) : null,
            EgenParameter3 = FormatDecimal(grandTotal),
            EgenParameter4 = FlowEngineCentraOrderMetadataHelper.MerchantReferenceFromParamsJson(order),
            EgenParameter5 = NormalizeOptional(order.PaymentMethod?.Name),
            EgenParameter6 = FlowEngineCentraOrderMetadataHelper.PaymentMethodNameFromParamsJson(order),
            EgenParameter7 = FlowEngineCentraOrderMetadataHelper.SuccessfulPaymentReference(order),
            EgenParameter8 = FlowEngineCentraOrderMetadataHelper.OrderAttributeValue(order, "pickup"),
            EgenParameter9 = FlowEngineCentraOrderMetadataHelper.InstaboxAvailabilityToken(order),
            EgenParameter10 = null,
            OrderLines = MapOrderLines(order, config)
        };
    }

    private static List<JeevesCreateOrderLineRequest> MapOrderLines(CentraRawOrder order, FlowEngineCentraOrderStoreConfig config)
    {
        var result = new List<JeevesCreateOrderLineRequest>();
        var storeId = order.Store?.Id ?? 0;
        var useJeevesDiscountForProducts = storeId == 1;

        foreach (var line in order.Lines)
        {
            var quantity = line.Quantity;
            var lineValueInclVat = line.LineValue?.Value ?? 0m;
            var taxPercent = line.TaxPercent;

            if (useJeevesDiscountForProducts)
            {
                var grossPriceExVat = line.UnitOriginalPrice?.Value ?? 0m;
                var unitNetExVat = 0m;
                if (quantity > 0m && lineValueInclVat > 0m)
                {
                    var unitInclVat = lineValueInclVat / quantity;
                    if (taxPercent >= 0m)
                        unitNetExVat = unitInclVat / (1m + (taxPercent / 100m));
                }

                var rabatt1 = 0m;
                if (grossPriceExVat > 0m)
                    rabatt1 = (1m - (unitNetExVat / grossPriceExVat)) * 100m;

                rabatt1 = Clamp(RoundDecimal(rabatt1, 2), 0m, 100m);
                if (Math.Abs(rabatt1) < 0.01m)
                    rabatt1 = 0m;

                result.Add(new JeevesCreateOrderLineRequest
                {
                    ArticleNumber = NormalizeProductArticleNumber(line.ProductVariant?.VariantNumber),
                    Quantity = quantity,
                    Price = grossPriceExVat,
                    CurrencyValue = 0,
                    CustomerDiscount = 0,
                    OrderDiscount = 0,
                    Discount1 = rabatt1,
                    Discount2 = 0,
                    Discount3 = 0,
                    Edit = "FlowEngine"
                });

                continue;
            }

            var unitIncl = quantity > 0m
                ? RoundDecimal(lineValueInclVat / quantity, 2)
                : RoundDecimal(lineValueInclVat, 2);
            var unitEx = taxPercent > 0m
                ? RoundDecimal(unitIncl / (1m + (taxPercent / 100m)), 2)
                : unitIncl;
            var vbPris = config.UsePriceIncludingTax ? unitEx : unitIncl;
            decimal? vbPrisInklMoms = config.UsePriceIncludingTax ? unitIncl : null;

            result.Add(new JeevesCreateOrderLineRequest
            {
                ArticleNumber = NormalizeProductArticleNumber(line.ProductVariant?.VariantNumber),
                Quantity = quantity,
                Price = vbPris,
                PriceIncludingVat = vbPrisInklMoms,
                CurrencyValue = 0,
                CustomerDiscount = 0,
                OrderDiscount = 0,
                Discount1 = 0,
                Discount2 = 0,
                Discount3 = 0,
                Edit = "FlowEngine"
            });
        }

        if (config.IncludeShippingLine)
            AddAdditionalLines(order, result);

        return result;
    }

    private static void AddAdditionalLines(CentraRawOrder order, List<JeevesCreateOrderLineRequest> lines)
    {
        var shippingValue = order.Totals?.Shipping?.Value ?? 0m;
        if (shippingValue > 0m)
        {
            var shippingTaxValue = order.Totals?.ShippingTaxIncluded?.Value ?? 0m;
            var shippingExTax = shippingValue - shippingTaxValue;
            lines.Add(new JeevesCreateOrderLineRequest
            {
                ArticleNumber = "10",
                Quantity = 1m,
                Price = RoundDecimal(shippingExTax, 2),
                PriceIncludingVat = RoundDecimal(shippingValue, 2),
                CurrencyValue = 0,
                CustomerDiscount = 0,
                OrderDiscount = 0,
                Discount1 = 0,
                Discount2 = 0,
                Discount3 = 0,
                Edit = "FlowEngine"
            });
        }

        var handlingValue = order.Totals?.Handling?.Value ?? 0m;
        if (handlingValue > 0m)
        {
            var rounded = RoundDecimal(handlingValue, 2);
            lines.Add(new JeevesCreateOrderLineRequest
            {
                ArticleNumber = "52",
                Quantity = 1m,
                Price = rounded,
                PriceIncludingVat = rounded,
                CurrencyValue = 0,
                CustomerDiscount = 0,
                OrderDiscount = 0,
                Discount1 = 0,
                Discount2 = 0,
                Discount3 = 0,
                Edit = "FlowEngine"
            });
        }

        var discountValue = order.Totals?.Discounts?.Value ?? 0m;
        if (discountValue > 0m)
        {
            var rounded = RoundDecimal(discountValue, 2);
            lines.Add(new JeevesCreateOrderLineRequest
            {
                ArticleNumber = "51",
                Quantity = 1m,
                Price = rounded,
                PriceIncludingVat = rounded,
                CurrencyValue = 0,
                CustomerDiscount = 0,
                OrderDiscount = 0,
                Discount1 = 0,
                Discount2 = 0,
                Discount3 = 0,
                Edit = "FlowEngine"
            });
        }
    }

    private static string? NormalizeProductArticleNumber(string? articleNumber)
    {
        var normalized = NormalizeOptional(articleNumber);
        if (normalized is null)
            return null;
        return normalized.Length > 2 && normalized.EndsWith("-1", StringComparison.Ordinal)
            ? normalized[..^2]
            : normalized;
    }

    private static string? FormatOrderDate(string? createdAt)
    {
        var normalized = NormalizeOptional(createdAt);
        if (normalized is null)
            return null;

        var localDateTime = ExtractLocalDateTime(normalized);
        if (!string.IsNullOrWhiteSpace(localDateTime))
            return localDateTime;

        if (normalized.EndsWith("Z", StringComparison.Ordinal))
            return EnsureMillis(normalized);

        return normalized;
    }

    private static string? ExtractLocalDateTime(string value)
    {
        var tIndex = value.IndexOf('T');
        if (tIndex < 0 || tIndex >= value.Length - 1)
            return null;

        var suffix = value[(tIndex + 1)..];
        var plusIndex = suffix.IndexOf('+');
        var minusIndex = suffix.IndexOf('-');
        var cutIndex = plusIndex >= 0 && minusIndex >= 0 ? Math.Min(plusIndex, minusIndex) : Math.Max(plusIndex, minusIndex);
        if (cutIndex < 0)
            return null;

        var localDateTime = value[..(tIndex + 1 + cutIndex)];
        return localDateTime.Replace('T', ' ');
    }

    private static string EnsureMillis(string value)
    {
        if (value.Contains('.', StringComparison.Ordinal))
            return value.EndsWith("Z", StringComparison.Ordinal) ? value : value + "Z";

        return value.EndsWith("Z", StringComparison.Ordinal)
            ? value[..^1] + ".000Z"
            : value + ".000Z";
    }

    private static string FormatDecimal(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static decimal RoundDecimal(decimal value, int decimals)
        => decimal.Round(value, decimals, MidpointRounding.AwayFromZero);

    private static decimal Clamp(decimal value, decimal min, decimal max)
        => value < min ? min : value > max ? max : value;

    private static string? Truncate(string? value, int max)
    {
        var normalized = NormalizeOptional(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return normalized;
        return normalized.Length <= max ? normalized : normalized[..max];
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}

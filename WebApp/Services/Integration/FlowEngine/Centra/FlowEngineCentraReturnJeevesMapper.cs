using System.Globalization;
using WebApp.Services.Integration.FlowEngine.CentraSendReturnsContracts;

namespace WebApp.Services.Integration.FlowEngine;

internal static class FlowEngineCentraReturnJeevesMapper
{
    private const int CentraOriginJeevesCompanyCode = 1;
    private const int MaxAddressFieldLength = 72;

    public static JeevesCreateOrderRequest Map(CentraRawReturn returnData)
    {
        if (returnData.Id <= 0)
            throw new ReturnMappingException("Return id is required");

        var storeId = returnData.Store?.Id ?? 0;
        var config = FlowEngineCentraStoreConfigService.GetReturnConfig(storeId);
        var countryCode = ResolveCountryCode(returnData);
        var ftgNr = FlowEngineCentraStoreConfigService.ResolveReturnCustomerNumber(storeId, countryCode);
        var ordTyp = ResolveReturnOrderType(storeId, returnData.ReturnedToStock, returnData.Comment, countryCode, config.OrderType);
        var grandTotal = returnData.GrandTotal?.Value ?? 0m;
        var negativeGrandTotal = -grandTotal;
        var paymentInfo = FlowEngineCentraReturnPaymentInfoHelper.ExtractReturnPaymentInfo(returnData);
        var shippingAddress = returnData.Shipment?.ShippingAddress;

        return new JeevesCreateOrderRequest
        {
            CompanyCode = CentraOriginJeevesCompanyCode,
            CustomerNumber = ftgNr,
            ExternalOrderNumber = $"C{returnData.Id}",
            CustomerReference = returnData.Order?.Number ?? string.Empty,
            OrderDate = FormatOrderDate(returnData.CreatedAt),
            OrderType = ordTyp,
            CurrencyCode = returnData.GrandTotal?.Currency?.Code,
            PartialDeliveryAllowed = 0,
            Edit = "FlowEngine",
            DeliveryName = Truncate(FlowEngineCentraCommonHelper.ResolveDeliveryName(config, shippingAddress), MaxAddressFieldLength),
            DeliveryAddress1 = Truncate(shippingAddress?.Address1, MaxAddressFieldLength),
            DeliveryAddress2 = Truncate(shippingAddress?.Address2, MaxAddressFieldLength),
            DeliveryZipCode = shippingAddress?.ZipCode,
            DeliveryCity = Truncate(shippingAddress?.City, MaxAddressFieldLength),
            DeliveryCountryCode = countryCode,
            GoodsMark3 = shippingAddress?.Email,
            GoodsMark4 = shippingAddress?.PhoneNumber,
            EgenParameter1 = storeId == 1 ? returnData.Order?.Market?.Name : null,
            EgenParameter2 = storeId > 0 ? storeId.ToString(CultureInfo.InvariantCulture) : null,
            EgenParameter3 = FormatDecimal(negativeGrandTotal),
            EgenParameter4 = paymentInfo.MerchantReference,
            EgenParameter5 = paymentInfo.PaymentMethod,
            EgenParameter6 = paymentInfo.PaymentMethodName,
            EgenParameter7 = paymentInfo.PaymentReference,
            OrderLines = MapReturnLines(returnData)
        };
    }

    private static List<JeevesCreateOrderLineRequest> MapReturnLines(CentraRawReturn returnData)
    {
        var result = new List<JeevesCreateOrderLineRequest>();
        foreach (var line in returnData.Lines)
        {
            var quantity = line.OrderLine?.ReturnedQuantity ?? 0m;
            var lineValue = line.OrderLine?.LineValue?.Value ?? 0m;
            var unitPrice = quantity > 0m
                ? RoundDecimal(lineValue / quantity, 2)
                : RoundDecimal(lineValue, 2);
            var rabatt1 = unitPrice == 0m ? 100m : 0m;

            result.Add(new JeevesCreateOrderLineRequest
            {
                ArticleNumber = NormalizeProductArticleNumber(line.OrderLine?.ProductVariant?.VariantNumber),
                Quantity = -quantity,
                Price = unitPrice,
                CurrencyValue = 0,
                PriceIncludingVat = null,
                CustomerDiscount = 0,
                OrderDiscount = 0,
                Discount1 = rabatt1,
                Discount2 = 0,
                Discount3 = 0,
                Edit = "FlowEngine"
            });
        }

        AddAdditionalReturnLines(returnData, result);
        return result;
    }

    private static void AddAdditionalReturnLines(CentraRawReturn returnData, List<JeevesCreateOrderLineRequest> result)
    {
        var existingArticleNumbers = new HashSet<string>(
            result.Select(line => NormalizeOptional(line.ArticleNumber)).Where(value => !string.IsNullOrWhiteSpace(value))!,
            StringComparer.Ordinal);
        var totals = returnData.Totals;
        if (totals is null)
            return;

        if (!existingArticleNumbers.Contains("10"))
        {
            var shippingValue = totals.Shipping?.Value ?? 0m;
            if (shippingValue != 0m)
            {
                var shippingTaxValue = totals.ShippingTaxRules.FirstOrDefault()?.TaxIncluded?.Value ?? 0m;
                AddAdditionalLine(result, "10", -1m, shippingValue - shippingTaxValue, shippingValue);
            }
        }

        if (!existingArticleNumbers.Contains("52"))
        {
            var handlingValue = totals.Handling?.Value ?? 0m;
            if (handlingValue != 0m)
            {
                var handlingTaxValue = totals.HandlingTaxRules.FirstOrDefault()?.TaxIncluded?.Value ?? 0m;
                AddAdditionalLine(result, "52", -1m, handlingValue - handlingTaxValue, handlingValue);
            }
        }

        if (!existingArticleNumbers.Contains("51"))
        {
            var discountValue = totals.Discounts?.Value ?? 0m;
            if (discountValue != 0m)
            {
                var discountTaxValue = totals.DiscountTaxRules.FirstOrDefault()?.TaxIncluded?.Value ?? 0m;
                AddAdditionalLine(result, "51", -1m, discountValue - discountTaxValue, discountValue);
            }
        }

        if (!existingArticleNumbers.Contains("53"))
        {
            var returnCostValue = totals.ReturnCost?.Value ?? 0m;
            if (returnCostValue > 0m)
            {
                var returnCostTaxValue = totals.ReturnCostTaxRules.FirstOrDefault()?.TaxIncluded?.Value ?? 0m;
                AddAdditionalLine(result, "53", 1m, returnCostValue - returnCostTaxValue, returnCostValue);
            }
        }
    }

    private static void AddAdditionalLine(List<JeevesCreateOrderLineRequest> result, string articleNumber, decimal quantity, decimal exTaxValue, decimal includingTaxValue)
    {
        result.Add(new JeevesCreateOrderLineRequest
        {
            ArticleNumber = articleNumber,
            Quantity = quantity,
            Price = RoundDecimal(exTaxValue, 2),
            PriceIncludingVat = RoundDecimal(includingTaxValue, 2),
            CurrencyValue = 0,
            CustomerDiscount = 0,
            OrderDiscount = 0,
            Discount1 = 0,
            Discount2 = 0,
            Discount3 = 0,
            Edit = "FlowEngine"
        });
    }

    private static string? ResolveCountryCode(CentraRawReturn returnData)
        => NormalizeOptional(returnData.Shipment?.ShippingAddress?.Country?.Code)
           ?? NormalizeOptional(returnData.Order?.ShippingAddress?.Country?.Code);

    private static int ResolveReturnOrderType(int storeId, bool? returnedToStock, string? comment, string? countryCode, int fallback)
    {
        if (storeId != 1)
            return fallback;

        var trimmedComment = NormalizeOptional(comment);
        var isNorway = string.Equals(countryCode, "NO", StringComparison.OrdinalIgnoreCase);
        var returned = returnedToStock ?? false;

        if (returned && !string.IsNullOrWhiteSpace(trimmedComment) && trimmedComment.StartsWith("160", StringComparison.Ordinal))
            return 902;

        if (returned && !string.IsNullOrWhiteSpace(trimmedComment) && trimmedComment.StartsWith("200", StringComparison.Ordinal))
            return isNorway ? 201 : 101;

        if (returned)
            return isNorway ? 201 : 101;

        return 201;
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

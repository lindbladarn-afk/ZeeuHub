using System.Globalization;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineShopifyOrderMapper : IFlowEngineShopifyOrderMapper
{
    private const int ShopifyJeevesCompanyCode = 5;
    private const int ShopifyOrderType = 8;
    private const int PartialDeliveryNotAllowed = 0;
    private const int MaxAddressFieldLength = 72;
    private const string ShopifyShippingArticleNumber = "10";

    public FlowEngineShopifyJeevesOrderPayload MapToJeevesOrder(FlowEngineShopifyOrderMappingInput input)
    {
        var numericOrderId = Normalize(input.NumericId);
        if (string.IsNullOrWhiteSpace(numericOrderId))
            throw new InvalidOperationException("numericId cannot be resolved from GID or legacyResourceId");

        var shippingCountryCode = Normalize(input.ShippingAddress?.CountryCodeV2)?.ToUpperInvariant();
        var customerNumber = ResolveFtgNr(shippingCountryCode);
        var shippingInfo = ResolveShippingInfo(input);
        var orderLines = MapOrderLines(input, shippingInfo.Amount);
        if (orderLines.Count == 0)
            throw new InvalidOperationException("Order contains no mappable line items");

        var currencyCode = ResolveCurrencyCode(input, shippingInfo.CurrencyCode);
        var grandTotal = ResolveGrandTotal(input, shippingInfo.Amount);
        var customerName = BuildName(input.CustomerFirstName, input.CustomerLastName);
        var shippingName = BuildName(input.ShippingAddress?.FirstName, input.ShippingAddress?.LastName);
        var contactPhone = NormalizePhoneNumber(input.CustomerPhone) ?? NormalizePhoneNumber(input.ShippingAddress?.Phone);

        return new FlowEngineShopifyJeevesOrderPayload
        {
            CompanyCode = ShopifyJeevesCompanyCode,
            CustomerNumber = customerNumber,
            ExternalOrderNumber = numericOrderId,
            CustomerReference = SanitizeCustomerOrderReference(input.Name) ?? numericOrderId,
            OrderDate = FormatOrderDate(input.CreatedAt),
            OrderType = ShopifyOrderType,
            CurrencyCode = currencyCode,
            PartialDeliveryAllowed = PartialDeliveryNotAllowed,
            Edit = "FlowEngine",
            DeliveryName = Truncate(Normalize(input.ShippingAddress?.Company) ?? shippingName ?? customerName, MaxAddressFieldLength),
            DeliveryAddress1 = Truncate(input.ShippingAddress?.Address1, MaxAddressFieldLength),
            DeliveryAddress2 = Truncate(input.ShippingAddress?.Address2, MaxAddressFieldLength),
            DeliveryZipCode = Normalize(input.ShippingAddress?.Zip),
            DeliveryCity = Truncate(input.ShippingAddress?.City, MaxAddressFieldLength),
            DeliveryCountryCode = shippingCountryCode,
            GoodsMark3 = Normalize(input.CustomerEmail),
            GoodsMark4 = contactPhone,
            EgenParameter3 = grandTotal.HasValue ? FormatDecimal(grandTotal.Value) : null,
            OrderLines = orderLines
        };
    }

    private static ShippingInfo ResolveShippingInfo(FlowEngineShopifyOrderMappingInput input)
    {
        decimal total = 0;
        var hasTotal = false;
        string? currencyCode = null;

        foreach (var line in input.ShippingLines)
        {
            var amount = line.CurrentDiscountedAmount ?? line.DiscountedAmount ?? line.OriginalAmount;
            if (amount.HasValue)
            {
                total += amount.Value;
                hasTotal = true;
            }

            currencyCode ??= Normalize(line.CurrentDiscountedCurrencyCode)
                ?? Normalize(line.DiscountedCurrencyCode)
                ?? Normalize(line.OriginalCurrencyCode);
        }

        if (hasTotal)
            return new ShippingInfo(total, currencyCode);

        return new ShippingInfo(input.FallbackShippingAmount, Normalize(input.FallbackShippingCurrencyCode));
    }

    private static List<FlowEngineShopifyJeevesOrderLinePayload> MapOrderLines(FlowEngineShopifyOrderMappingInput input, decimal? shippingTotalAmount)
    {
        var result = new List<FlowEngineShopifyJeevesOrderLinePayload>();

        for (var index = 0; index < input.OrderLines.Count; index++)
        {
            var line = input.OrderLines[index];
            var articleNumber = Normalize(line.Sku) ?? Normalize(line.VariantSku);
            if (articleNumber is null)
                throw new InvalidOperationException($"Line {index + 1} is missing SKU/variant SKU");
            if (line.Quantity <= 0)
                throw new InvalidOperationException($"Line {index + 1} has invalid quantity");

            var quantity = Convert.ToDecimal(line.Quantity, CultureInfo.InvariantCulture);
            var unitPrice = ResolveUnitPrice(line.DiscountedTotalAmount, line.OriginalTotalAmount, quantity);

            result.Add(new FlowEngineShopifyJeevesOrderLinePayload
            {
                ArticleNumber = articleNumber,
                Quantity = quantity,
                Price = unitPrice,
                CurrencyValue = 0,
                CustomerDiscount = 0,
                OrderDiscount = 0,
                Discount1 = 0,
                Discount2 = 0,
                Discount3 = 0,
                Edit = "FlowEngine"
            });
        }

        if (shippingTotalAmount.HasValue && shippingTotalAmount.Value > 0)
        {
            result.Add(new FlowEngineShopifyJeevesOrderLinePayload
            {
                ArticleNumber = ShopifyShippingArticleNumber,
                Quantity = 1,
                Price = RoundAwayFromZero(shippingTotalAmount.Value, 2),
                CurrencyValue = 0,
                CustomerDiscount = 0,
                OrderDiscount = 0,
                Discount1 = 0,
                Discount2 = 0,
                Discount3 = 0,
                Edit = "FlowEngine"
            });
        }

        return result;
    }

    private static decimal? ResolveUnitPrice(decimal? discountedTotal, decimal? originalTotal, decimal quantity)
    {
        if (discountedTotal.HasValue && discountedTotal.Value > 0 && quantity > 0)
            return RoundAwayFromZero(discountedTotal.Value / quantity, 2);
        if (originalTotal.HasValue && originalTotal.Value > 0 && quantity > 0)
            return RoundAwayFromZero(originalTotal.Value / quantity, 2);

        return null;
    }

    private static decimal? ResolveGrandTotal(FlowEngineShopifyOrderMappingInput input, decimal? shippingTotalAmount)
    {
        decimal sum = 0;
        var hasValue = false;

        foreach (var line in input.OrderLines)
        {
            var total = line.DiscountedTotalAmount ?? line.OriginalTotalAmount;
            if (!total.HasValue)
                continue;

            sum += total.Value;
            hasValue = true;
        }

        if (shippingTotalAmount.HasValue && shippingTotalAmount.Value > 0)
        {
            sum += shippingTotalAmount.Value;
            hasValue = true;
        }

        return hasValue ? RoundAwayFromZero(sum, 2) : null;
    }

    private static string? ResolveCurrencyCode(FlowEngineShopifyOrderMappingInput input, string? shippingCurrencyCode)
    {
        foreach (var line in input.OrderLines)
        {
            var currency = Normalize(line.DiscountedTotalCurrencyCode) ?? Normalize(line.OriginalTotalCurrencyCode);
            if (!string.IsNullOrWhiteSpace(currency))
                return currency;
        }

        return Normalize(shippingCurrencyCode);
    }

    private static string ResolveFtgNr(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            throw new InvalidOperationException("shippingAddress.countryCodeV2 is required for ftgnr mapping");

        return countryCode.ToUpperInvariant() switch
        {
            "SE" => "15307",
            _ => throw new InvalidOperationException($"No ftgnr mapping configured for country code '{countryCode.ToUpperInvariant()}'")
        };
    }

    private static string? FormatOrderDate(string? createdAt)
    {
        var normalized = Normalize(createdAt);
        if (normalized is null)
            return null;

        var tIndex = normalized.IndexOf('T');
        if (tIndex >= 0)
        {
            var offsetIndex = normalized.IndexOf('+', tIndex + 1);
            if (offsetIndex < 0)
                offsetIndex = normalized.IndexOf('-', tIndex + 1);

            if (offsetIndex > tIndex)
                normalized = normalized[..offsetIndex];
        }

        if (normalized.EndsWith('Z'))
            return normalized.Contains('.') ? normalized : normalized.Replace("Z", ".000Z", StringComparison.Ordinal);

        return normalized.Contains('.') ? normalized + "Z" : normalized + ".000Z";
    }

    private static string? BuildName(string? firstName, string? lastName)
    {
        var parts = new[] { Normalize(firstName), Normalize(lastName) }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!);
        var joined = string.Join(' ', parts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private static string? SanitizeCustomerOrderReference(string? value)
    {
        var normalized = Normalize(value);
        if (normalized is null)
            return null;

        while (normalized.StartsWith('#'))
            normalized = normalized[1..].Trim();

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizePhoneNumber(string? value)
    {
        var normalized = Normalize(value);
        if (normalized is null)
            return null;

        var digits = new string(normalized.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            return null;
        if (digits.StartsWith("0046", StringComparison.Ordinal))
            return "0" + digits[4..];
        if (digits.StartsWith("46", StringComparison.Ordinal))
        {
            var rest = digits[2..];
            return rest.StartsWith("0", StringComparison.Ordinal) ? rest : "0" + rest;
        }

        return digits;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        var normalized = Normalize(value);
        if (normalized is null)
            return null;

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string FormatDecimal(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static decimal RoundAwayFromZero(decimal value, int scale)
        => Math.Round(value, scale, MidpointRounding.AwayFromZero);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ShippingInfo(decimal? Amount, string? CurrencyCode);
}

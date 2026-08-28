using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebApp.Services.Integration.FlowEngine.CentraSendReturnsContracts;

internal sealed class ReturnPaymentInfo
{
    public string? MerchantReference { get; set; }
    public string? PaymentReference { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentMethodName { get; set; }
}

internal sealed class ReturnMappingException : Exception
{
    public ReturnMappingException(string message) : base(message)
    {
    }
}

internal sealed class CentraReturnsResponse
{
    public CentraReturnsData? Data { get; set; }
}

internal sealed class CentraReturnsData
{
    public List<CentraRawReturn>? Returns { get; set; }
}

internal sealed class CentraRawReturn
{
    public int Id { get; set; }
    public string? CreatedAt { get; set; }
    public string? ReturnStatus { get; set; }
    public bool? ReturnedToStock { get; set; }
    public CentraStore? Store { get; set; }
    public CentraReturnShipment? Shipment { get; set; }
    public List<CentraRefundPaymentHistory> RefundPaymentHistory { get; set; } = new();
    public CentraGrandTotal? GrandTotal { get; set; }
    public List<CentraReturnLine> Lines { get; set; } = new();
    public CentraReturnTotals? Totals { get; set; }
    public string? Comment { get; set; }
    public CentraReturnOrder? Order { get; set; }
}

internal sealed class CentraStore
{
    public int Id { get; set; }
}

internal sealed class CentraReturnShipment
{
    public CentraShippingAddress? ShippingAddress { get; set; }
}

internal sealed class CentraReturnLine
{
    public CentraReturnOrderLine? OrderLine { get; set; }
}

internal sealed class CentraReturnOrderLine
{
    public CentraMoney? LineValue { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public CentraProductVariant? ProductVariant { get; set; }
}

internal sealed class CentraReturnOrder
{
    public string? Id { get; set; }

    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Number { get; set; }

    public CentraMarket? Market { get; set; }
    public CentraShippingAddress? ShippingAddress { get; set; }
    public List<CentraPaymentHistory>? PaymentHistory { get; set; }
    public CentraPaymentMethod? PaymentMethod { get; set; }
    public List<CentraAttributeGroup> Attributes { get; set; } = new();
}

internal sealed class CentraMarket
{
    public string? Name { get; set; }
}

internal sealed class CentraShippingAddress
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? CompanyName { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? ZipCode { get; set; }
    public string? City { get; set; }
    public CentraCountry? Country { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}

internal sealed class CentraCountry
{
    public string? Code { get; set; }
}

internal sealed class CentraRefundPaymentHistory
{
    public string? EntryType { get; set; }
    public CentraMoney? Value { get; set; }

    [JsonPropertyName("paramsJSON")]
    public string? ParamsJson { get; set; }

    public string? PaymentMethod { get; set; }
}

internal sealed class CentraPaymentHistory
{
    public string? EntryType { get; set; }
    public string? Status { get; set; }
    public string? ExternalReference { get; set; }
    public CentraMoney? Value { get; set; }

    [JsonPropertyName("paramsJSON")]
    public string? ParamsJson { get; set; }
}

internal sealed class CentraPaymentMethod
{
    public string? Name { get; set; }
}

internal sealed class CentraAttributeGroup
{
    public List<CentraAttributeElement> Elements { get; set; } = new();
}

internal sealed class CentraAttributeElement
{
    public string? Key { get; set; }
    public string? Value { get; set; }
    public string? Description { get; set; }
}

internal sealed class CentraReturnTotals
{
    public CentraMoney? Shipping { get; set; }
    public List<CentraTaxRule> ShippingTaxRules { get; set; } = new();
    public CentraMoney? Handling { get; set; }
    public List<CentraTaxRule> HandlingTaxRules { get; set; } = new();
    public CentraMoney? ReturnCost { get; set; }
    public List<CentraTaxRule> ReturnCostTaxRules { get; set; } = new();
    public CentraMoney? Discounts { get; set; }
    public List<CentraTaxRule> DiscountTaxRules { get; set; } = new();
}

internal sealed class CentraTaxRule
{
    public CentraMoney? TaxIncluded { get; set; }
}

internal sealed class CentraGrandTotal
{
    public decimal Value { get; set; }
    public CentraCurrency? Currency { get; set; }
}

internal sealed class CentraCurrency
{
    public string? Code { get; set; }
}

internal sealed class CentraMoney
{
    public decimal Value { get; set; }
}

internal sealed class CentraProductVariant
{
    public string? VariantNumber { get; set; }
}

internal sealed class JeevesCreateOrderRequest
{
    [JsonPropertyName("c_foretagkod")]
    public int CompanyCode { get; set; }

    [JsonPropertyName("c_ftgnr")]
    public string? CustomerNumber { get; set; }

    [JsonPropertyName("c_extordernr")]
    public string ExternalOrderNumber { get; set; } = string.Empty;

    [JsonPropertyName("c_kundbestnr")]
    public string CustomerReference { get; set; } = string.Empty;

    [JsonPropertyName("c_orddatum")]
    public string? OrderDate { get; set; }

    [JsonPropertyName("c_OrdTyp")]
    public int OrderType { get; set; }

    [JsonPropertyName("c_valkod")]
    public string? CurrencyCode { get; set; }

    [JsonPropertyName("c_dellevtillaten")]
    public int PartialDeliveryAllowed { get; set; }

    [JsonPropertyName("c_edit")]
    public string Edit { get; set; } = "FlowEngine";

    [JsonPropertyName("c_ordlevadr1")]
    public string? DeliveryName { get; set; }

    [JsonPropertyName("c_ordlevadr2")]
    public string? DeliveryAddress1 { get; set; }

    [JsonPropertyName("c_ordlevadr3")]
    public string? DeliveryAddress2 { get; set; }

    [JsonPropertyName("c_ordlevadr4")]
    public string? DeliveryZipCode { get; set; }

    [JsonPropertyName("c_ordlevadrbstort")]
    public string? DeliveryCity { get; set; }

    [JsonPropertyName("c_ordlevadrlandskod")]
    public string? DeliveryCountryCode { get; set; }

    [JsonPropertyName("c_godsMarke3")]
    public string? GoodsMark3 { get; set; }

    [JsonPropertyName("c_godsMarke4")]
    public string? GoodsMark4 { get; set; }

    [JsonPropertyName("c_egenparameter1")]
    public string? EgenParameter1 { get; set; }

    [JsonPropertyName("c_egenparameter2")]
    public string? EgenParameter2 { get; set; }

    [JsonPropertyName("c_egenparameter3")]
    public string? EgenParameter3 { get; set; }

    [JsonPropertyName("c_egenparameter4")]
    public string? EgenParameter4 { get; set; }

    [JsonPropertyName("c_egenparameter5")]
    public string? EgenParameter5 { get; set; }

    [JsonPropertyName("c_egenparameter6")]
    public string? EgenParameter6 { get; set; }

    [JsonPropertyName("c_egenparameter7")]
    public string? EgenParameter7 { get; set; }

    [JsonPropertyName("OrderRader")]
    public List<JeevesCreateOrderLineRequest> OrderLines { get; set; } = new();
}

internal sealed class JeevesCreateOrderLineRequest
{
    [JsonPropertyName("c_artnr")]
    public string? ArticleNumber { get; set; }

    [JsonPropertyName("c_ordantal")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("c_vb_pris")]
    public decimal? Price { get; set; }

    [JsonPropertyName("c_valkod")]
    public int CurrencyValue { get; set; }

    [JsonPropertyName("c_vb_prisinklmoms")]
    public decimal? PriceIncludingVat { get; set; }

    [JsonPropertyName("c_kundrabatt")]
    public decimal CustomerDiscount { get; set; }

    [JsonPropertyName("c_OrdRabatt")]
    public decimal OrderDiscount { get; set; }

    [JsonPropertyName("c_rabatt1")]
    public decimal? Discount1 { get; set; }

    [JsonPropertyName("c_rabatt2")]
    public decimal? Discount2 { get; set; }

    [JsonPropertyName("c_rabatt3")]
    public decimal? Discount3 { get; set; }

    [JsonPropertyName("c_edit")]
    public string Edit { get; set; } = "FlowEngine";
}

internal sealed class FlexibleStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var intValue)
                ? intValue.ToString(CultureInfo.InvariantCulture)
                : reader.GetDecimal().ToString(CultureInfo.InvariantCulture),
            _ => null
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value);
    }
}

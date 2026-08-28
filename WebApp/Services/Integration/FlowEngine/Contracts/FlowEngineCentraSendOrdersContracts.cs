using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebApp.Services.Integration.FlowEngine.CentraSendOrdersContracts;

internal sealed class CentraOrdersResponse
{
    public CentraOrdersData? Data { get; set; }
}

internal sealed class CentraOrderByIdResponse
{
    public CentraOrderByIdData? Data { get; set; }
}

internal sealed class CentraOrderByIdData
{
    public CentraRawOrder? Order { get; set; }
}

internal sealed class CentraOrdersData
{
    public List<CentraRawOrder>? Orders { get; set; }
}

internal sealed class CentraRawOrder
{
    public string Id { get; set; } = string.Empty;

    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Number { get; set; }

    public string? Status { get; set; }
    public string? CreatedAt { get; set; }
    public CentraStore? Store { get; set; }
    public CentraMarket? Market { get; set; }
    public CentraShippingAddress? ShippingAddress { get; set; }
    public string? InternalComment { get; set; }
    public List<CentraAttributeGroup> Attributes { get; set; } = new();
    public CentraTotals? Totals { get; set; }
    public List<CentraPaymentHistory> PaymentHistory { get; set; } = new();
    public CentraPaymentMethod? PaymentMethod { get; set; }
    public CentraGrandTotal? GrandTotal { get; set; }
    public List<CentraOrderLine> Lines { get; set; } = new();
    public CentraAccount? Account { get; set; }
}

internal sealed class CentraStore
{
    public int Id { get; set; }
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
    public string? City { get; set; }
    public string? ZipCode { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public CentraCountry? Country { get; set; }
}

internal sealed class CentraCountry
{
    public string? Code { get; set; }
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

internal sealed class CentraTotals
{
    public CentraMoney? Shipping { get; set; }
    public CentraMoney? ShippingTaxIncluded { get; set; }
    public CentraMoney? Handling { get; set; }
    public CentraMoney? Discounts { get; set; }
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

internal sealed class CentraGrandTotal
{
    public decimal Value { get; set; }
    public CentraCurrency? Currency { get; set; }
}

internal sealed class CentraCurrency
{
    public string? Code { get; set; }
}

internal sealed class CentraOrderLine
{
    public CentraProductVariant? ProductVariant { get; set; }
    public decimal Quantity { get; set; }
    public CentraMoney? LineValue { get; set; }
    public decimal TaxPercent { get; set; }
    public CentraMoney? UnitOriginalPrice { get; set; }
}

internal sealed class CentraProductVariant
{
    public string? VariantNumber { get; set; }
}

internal sealed class CentraMoney
{
    public decimal Value { get; set; }
}

internal sealed class CentraAccount
{
    [JsonConverter(typeof(CentraAccountAttributesConverter))]
    public List<CentraAttributeElement> Attributes { get; set; } = new();
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

    [JsonPropertyName("c_levsattkod")]
    public int? DeliveryMethodCode { get; set; }

    [JsonPropertyName("c_valkod")]
    public string? CurrencyCode { get; set; }

    [JsonPropertyName("c_BetKod")]
    public string? PaymentCode { get; set; }

    [JsonPropertyName("c_dellevtillaten")]
    public int PartialDeliveryAllowed { get; set; }

    [JsonPropertyName("c_edit")]
    public string Edit { get; set; } = "FlowEngine";

    [JsonPropertyName("c_ordlevplats1")]
    public string? DeliveryPlace1 { get; set; }

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

    [JsonPropertyName("c_ftgpostnr")]
    public string? CompanyPostNumber { get; set; }

    [JsonPropertyName("c_kundref2")]
    public string? CustomerReference2 { get; set; }

    [JsonPropertyName("c_godsMarke1")]
    public string? GoodsMark1 { get; set; }

    [JsonPropertyName("c_godsMarke2")]
    public string? GoodsMark2 { get; set; }

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

    [JsonPropertyName("c_egenparameter8")]
    public string? EgenParameter8 { get; set; }

    [JsonPropertyName("c_egenparameter9")]
    public string? EgenParameter9 { get; set; }

    [JsonPropertyName("c_egenparameter10")]
    public string? EgenParameter10 { get; set; }

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

internal sealed class CentraAccountAttributesConverter : JsonConverter<List<CentraAttributeElement>>
{
    public override List<CentraAttributeElement> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var result = new List<CentraAttributeElement>();

        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            FlowEngineCentraJsonElementReader.TryGetPropertyCaseInsensitive(document.RootElement, "elements", out var elements) &&
            elements.ValueKind == JsonValueKind.Array)
        {
            result.AddRange(JsonSerializer.Deserialize<List<CentraAttributeElement>>(elements.GetRawText(), options) ?? new List<CentraAttributeElement>());
            return result;
        }

        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object &&
                    FlowEngineCentraJsonElementReader.TryGetPropertyCaseInsensitive(item, "elements", out var groupElements) &&
                    groupElements.ValueKind == JsonValueKind.Array)
                {
                    result.AddRange(JsonSerializer.Deserialize<List<CentraAttributeElement>>(groupElements.GetRawText(), options) ?? new List<CentraAttributeElement>());
                }
            }
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, List<CentraAttributeElement> value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}

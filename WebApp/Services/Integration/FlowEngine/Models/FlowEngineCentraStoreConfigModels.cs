namespace WebApp.Services.Integration.FlowEngine;

public sealed record FlowEngineCentraOrderStoreConfig(
    int OrderType,
    bool UseAccountAttributeForFtgNr,
    bool UseCompanyNameForAddress,
    bool UseInternalCommentAsCustomerReference,
    bool IncludeShippingLine,
    bool UseDeliveryMethodCodeFromAttributes,
    bool UsePriceIncludingTax);

public sealed record FlowEngineCentraReturnStoreConfig(
    int OrderType,
    bool UseCompanyNameForAddress);

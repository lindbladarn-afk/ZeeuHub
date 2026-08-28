using WebApp.Services.Integration.FlowEngine.CentraSendOrdersContracts;

namespace WebApp.Services.Integration.FlowEngine;

internal static class FlowEngineCentraStoreConfigService
{
    private static readonly IReadOnlyDictionary<string, string> Store1FtgNr = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["SE"] = "14943", ["EU"] = "15087", ["LU"] = "15908", ["GB"] = "15088", ["NO"] = "15559",
        ["DK"] = "15560", ["NL"] = "15684", ["BE"] = "15685", ["FI"] = "15686", ["DE"] = "15839",
        ["FR"] = "15896", ["IT"] = "15897", ["ES"] = "15898", ["IE"] = "15899", ["AT"] = "15900",
        ["GR"] = "15901", ["LT"] = "15902", ["EE"] = "15903", ["PL"] = "15904", ["HR"] = "15905",
        ["CZ"] = "15906", ["SK"] = "15907", ["CY"] = "15909", ["SI"] = "15910", ["HU"] = "15911",
        ["BG"] = "15912", ["LV"] = "15913"
    };

    private static readonly IReadOnlyDictionary<string, string> Store4FtgNr = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["FI"] = "16779", ["NL"] = "16697", ["IT"] = "16696", ["DK"] = "16695", ["DE"] = "16694",
        ["BE"] = "16693", ["AT"] = "16692", ["EU"] = "16566", ["SE"] = "16565"
    };

    public static FlowEngineCentraOrderStoreConfig GetOrderConfig(int storeId)
        => storeId switch
        {
            1 => new FlowEngineCentraOrderStoreConfig(7, false, false, false, true, true, true),
            2 => new FlowEngineCentraOrderStoreConfig(1, true, true, false, false, false, false),
            4 => new FlowEngineCentraOrderStoreConfig(17, false, false, true, false, true, false),
            _ => new FlowEngineCentraOrderStoreConfig(0, false, false, false, false, true, false)
        };

    public static FlowEngineCentraReturnStoreConfig GetReturnConfig(int storeId)
        => storeId switch
        {
            1 => new FlowEngineCentraReturnStoreConfig(7, false),
            2 => new FlowEngineCentraReturnStoreConfig(1, true),
            4 => new FlowEngineCentraReturnStoreConfig(17, false),
            _ => new FlowEngineCentraReturnStoreConfig(0, false)
        };

    public static string? ResolveOrderCustomerNumber(FlowEngineCentraOrderStoreConfig config, int storeId, string? countryCode, CentraRawOrder order)
    {
        if (config.UseAccountAttributeForFtgNr)
            return order.Account?.Attributes
                .Select(element => NormalizeOptional(element.Value))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return ResolveFromCompanyAndCountry(storeId, countryCode);
    }

    public static string? ResolveReturnCustomerNumber(int storeId, string? countryCode)
        => ResolveFromCompanyAndCountry(storeId, countryCode);

    public static string? GetOrderCustomerNumber(CentraRawOrder order)
    {
        foreach (var element in order.Account?.Attributes ?? Enumerable.Empty<CentraAttributeElement>())
        {
            var description = element.Description?.Trim().ToLowerInvariant();
            var key = element.Key?.Trim().ToLowerInvariant();
            if (description == "customer number" || key == "customer number")
                return NormalizeOptional(element.Value);
        }

        return null;
    }

    private static string? ResolveFromCompanyAndCountry(int storeId, string? countryCode)
    {
        var ftgNr = FtgNr(storeId, countryCode);
        if (ftgNr is null && !string.IsNullOrWhiteSpace(countryCode))
            ftgNr = FtgNr(storeId, "EU");
        return ftgNr;
    }

    private static string? FtgNr(int storeId, string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return null;

        var key = countryCode.Trim().ToUpperInvariant();
        return storeId switch
        {
            1 => Store1FtgNr.TryGetValue(key, out var value) ? value : null,
            4 => Store4FtgNr.TryGetValue(key, out var value) ? value : null,
            _ => null
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}

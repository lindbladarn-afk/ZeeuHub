using System.Collections.ObjectModel;
using WebApp.Models.Application;

namespace WebApp.Services.ExcelImport;

// Centralizes Excel import type metadata used by the controller, runtime status, and edit UI.
public sealed class ExcelImportTypeDefinition
{
    public required string ImportType { get; init; }
    public required string DisplayName { get; init; }
    public IReadOnlyList<string> RequiredHeaders { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NumericHeaders { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PercentHeaders { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ZeroOrOneHeaders { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ThreeLetterCodeHeaders { get; init; } = Array.Empty<string>();
    public bool RequireVoucherDebitOrCredit { get; init; }
    public bool ValidateBudgetPeriod { get; init; }
}

public static class ExcelImportTypeDefinitions
{
    private static readonly IReadOnlyDictionary<string, ExcelImportTypeDefinition> Definitions =
        new ReadOnlyDictionary<string, ExcelImportTypeDefinition>(
            new Dictionary<string, ExcelImportTypeDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["voucher"] = new()
                {
                    ImportType = "voucher",
                    DisplayName = "Voucher",
                    RequiredHeaders = new[] { "Account" },
                    NumericHeaders = new[] { "Debit", "Credit" },
                    RequireVoucherDebitOrCredit = true
                },
                ["budget"] = new()
                {
                    ImportType = "budget",
                    DisplayName = "Budget",
                    RequiredHeaders = new[] { "Account", "Amount" },
                    NumericHeaders = new[] { "Amount" },
                    ValidateBudgetPeriod = true
                },
                ["purchaseprice"] = new()
                {
                    ImportType = "purchaseprice",
                    DisplayName = "Inköpspriser",
                    RequiredHeaders = new[] { "ArtNr", "Inpris brutto valuta" },
                    NumericHeaders = new[] { "Inpris brutto valuta" },
                    PercentHeaders = new[] { "Rabatt %", "Hemtagn. %", "Fraktkost %" }
                },
                ["priceupdate"] = new()
                {
                    ImportType = "priceupdate",
                    DisplayName = "Prisuppdatering",
                    RequiredHeaders = new[] { "Artnr", "Pris" },
                    NumericHeaders = new[] { "Pris", "Antalgräns", "Rabatt %", "Nytt pris", "Nettopris ej rabatt (1/0)", "Matrisrabatt (1/0)" },
                    ZeroOrOneHeaders = new[] { "Nettopris ej rabatt (1/0)", "Matrisrabatt (1/0)" },
                    ThreeLetterCodeHeaders = new[] { "Valutakod" }
                },
                ["transautoprice"] = new()
                {
                    ImportType = "transautoprice",
                    DisplayName = "Prisinläsning Trans Auto",
                    RequiredHeaders = new[] { "Supplier", "SupplierArticleNo", "CurrencyCode", "ListPrice" },
                    NumericHeaders = new[]
                    {
                        "ListPrice",
                        "NetPrice",
                        "DiscountPercent",
                        "MinimumOrderQuantity",
                        "PackageQuantity",
                        "WeightKg"
                    },
                    ThreeLetterCodeHeaders = new[] { "CurrencyCode" }
                },
                ["presskogyoprice"] = new()
                {
                    ImportType = "presskogyoprice",
                    DisplayName = "Prisinläsning Press Kogyo",
                    RequiredHeaders = new[] { "Supplier", "SupplierArticleNo", "CustomerArticleNo", "CurrencyCode", "ListPrice" },
                    NumericHeaders = new[]
                    {
                        "ListPrice",
                        "NetPrice",
                        "DiscountPercent",
                        "MinimumOrderQuantity",
                        "PackageQuantity",
                        "WeightKg"
                    },
                    ThreeLetterCodeHeaders = new[] { "CurrencyCode" }
                }
            });

    public static ExcelImportTypeDefinition Get(string? importType)
    {
        var key = Normalize(importType);
        return Definitions.TryGetValue(key, out var definition)
            ? definition
            : new ExcelImportTypeDefinition
            {
                ImportType = key.Length == 0 ? "excelimport" : key,
                DisplayName = "Excelimport"
            };
    }

    public static string GetDisplayName(string? importType)
        => Get(importType).DisplayName;

    public static bool IsKnown(string? importType)
        => Definitions.ContainsKey(Normalize(importType));

    public static string Normalize(string? importType)
        => (importType ?? string.Empty).Trim().ToLowerInvariant();

    public static string ResolveRuntimeImportType(SidebarRuntimeStatusItemViewModel item)
    {
        var aggregateKey = item.AggregateKey ?? string.Empty;
        var parts = aggregateKey.Split(':', StringSplitOptions.RemoveEmptyEntries);
        var typeKey = parts.Length >= 2 ? parts[1].Trim().ToLowerInvariant() : string.Empty;

        return typeKey switch
        {
            _ when IsKnown(typeKey) => typeKey,
            _ => ResolveRuntimeImportTypeFromText(item)
        };
    }

    private static string ResolveRuntimeImportTypeFromText(SidebarRuntimeStatusItemViewModel item)
    {
        var summary = item.Summary ?? string.Empty;
        if (summary.Contains("Voucher", StringComparison.OrdinalIgnoreCase) || item.Title.Contains("Voucher", StringComparison.OrdinalIgnoreCase))
            return "voucher";
        if (summary.Contains("Budget", StringComparison.OrdinalIgnoreCase) || item.Title.Contains("Budget", StringComparison.OrdinalIgnoreCase))
            return "budget";
        if (summary.Contains("Inköpspriser", StringComparison.OrdinalIgnoreCase) || item.Title.Contains("Inköpspriser", StringComparison.OrdinalIgnoreCase))
            return "purchaseprice";
        if (summary.Contains("Prisuppdatering", StringComparison.OrdinalIgnoreCase) || item.Title.Contains("Prisuppdatering", StringComparison.OrdinalIgnoreCase))
            return "priceupdate";
        if (summary.Contains("Trans Auto", StringComparison.OrdinalIgnoreCase) || item.Title.Contains("Trans Auto", StringComparison.OrdinalIgnoreCase))
            return "transautoprice";
        if (summary.Contains("Press Kogyo", StringComparison.OrdinalIgnoreCase) || item.Title.Contains("Press Kogyo", StringComparison.OrdinalIgnoreCase))
            return "presskogyoprice";

        return string.Empty;
    }
}

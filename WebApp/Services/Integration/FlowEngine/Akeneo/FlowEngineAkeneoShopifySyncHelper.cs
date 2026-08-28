using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WebApp.Models.Integration;
using WebApp.Services.Integration.Akeneo;

namespace WebApp.Services.Integration.FlowEngine;

internal static class FlowEngineAkeneoShopifySyncHelper
{
    internal const string StatusEligible = "eligible";
    internal const string StatusSkipped = "skipped";
    internal const string StatusFailed = "failed";

    private const string CodeShopifySyncDisabled = "SHP-PROD-VAL-001";
    private const string CodeMissingSku = "SHP-PROD-VAL-002";
    private const string CodeMissingTitle = "SHP-PROD-VAL-003";

    private const string WarningInvalidGtin = "SHP-PROD-WARN-001";
    private const string WarningUnmappedProductType = "SHP-PROD-WARN-002";
    private const string WarningImageTokensUnresolved = "SHP-PROD-WARN-003";
    private const string WarningShopifyProductNotFound = "SHP-PROD-WARN-004";
    private const string WarningDescriptionLargeChange = "SHP-PROD-WARN-005";

    internal static bool HasChanges(FlowEngineAkeneoShopifyUpdateDraft draft)
        => !string.IsNullOrWhiteSpace(draft.VariantBarcode) ||
           draft.TagsToAdd.Count > 0 ||
           draft.MetafieldsToUpsert.Count > 0;

    internal static SyncDecision BuildDecision(AkeneoProduct product, string? title)
    {
        var normalizedSku = Normalize(product.Identifier);
        if (normalizedSku is null)
            return new SyncDecision(StatusFailed, CodeMissingSku, "Missing required SKU");

        if (Normalize(product.ShopifySync) != "1")
            return new SyncDecision(StatusSkipped, CodeShopifySyncDisabled, "Product skipped because shopify_sync is not set to 1");

        if (title is null)
            return new SyncDecision(StatusFailed, CodeMissingTitle, "Missing required title");

        return new SyncDecision(StatusEligible, null, null);
    }

    internal static DesiredDraftResult BuildDesiredDraft(AkeneoProduct product)
    {
        var sku = Normalize(product.Identifier) ?? string.Empty;
        var title = PreferredTitle(product);
        var warnings = new List<FlowEngineAkeneoSendToShopifyWarning>();

        string? productType = null;
        if (Normalize(product.ArtKat) is not null)
        {
            warnings.Add(new FlowEngineAkeneoSendToShopifyWarning
            {
                Code = WarningUnmappedProductType,
                Message = "Product type mapping is not locked for ArtKat/VaruGruppKod"
            });
        }

        var imageTokens = SplitCsv(product.MainImage);
        if (imageTokens.Count > 0)
        {
            warnings.Add(new FlowEngineAkeneoSendToShopifyWarning
            {
                Code = WarningImageTokensUnresolved,
                Message = "Image tokens are present but URL resolver is not enabled in this stage"
            });
        }

        string? barcode = null;
        var rawEan = Normalize(product.ArtNrEan);
        if (rawEan is not null)
        {
            if (IsValidGtin(rawEan))
            {
                barcode = NormalizeBarcode(rawEan);
            }
            else
            {
                warnings.Add(new FlowEngineAkeneoSendToShopifyWarning
                {
                    Code = WarningInvalidGtin,
                    Message = "Barcode was ignored because ArtNrEAN is not a valid GTIN"
                });
            }
        }

        var draft = new FlowEngineAkeneoShopifyDraft
        {
            Sku = sku,
            Product = new FlowEngineAkeneoShopifyProductDraft
            {
                Title = title,
                Handle = BuildHandle(title),
                Vendor = Normalize(product.ArtRitnNr),
                ProductType = productType,
                Status = "ACTIVE",
                Tags = NormalizeTags(SplitCsv(product.Directive)),
                DescriptionHtml = Normalize(product.DescriptionLong),
                ImageTokens = imageTokens.OrderBy(value => value, StringComparer.Ordinal).ToList(),
                ImageUrls = new List<string>()
            },
            Variant = new FlowEngineAkeneoShopifyVariantDraft
            {
                Sku = sku,
                Barcode = barcode
            },
            Metafields = NormalizeMetafields(BuildAkeneoMetafields(product))
        };

        return new DesiredDraftResult(draft, warnings.OrderBy(warning => warning.Code, StringComparer.Ordinal).ToList());
    }

    internal static FlowEngineAkeneoSendToShopifyUpdatePlan Diff(
        FlowEngineAkeneoShopifyDraft desired,
        FlowEngineAkeneoShopifyDraft? current)
    {
        var desiredTags = NormalizeTags(desired.Product.Tags);
        var desiredMetafields = NormalizeMetafields(desired.Metafields);

        if (current is null)
        {
            return new FlowEngineAkeneoSendToShopifyUpdatePlan
            {
                WouldCreate = true,
                NoChange = false,
                WouldUpdate = new FlowEngineAkeneoShopifyUpdateDraft(),
                Warnings =
                {
                    new FlowEngineAkeneoSendToShopifyWarning
                    {
                        Code = WarningShopifyProductNotFound,
                        Message = $"No Shopify product matched SKU {desired.Sku}; create would be required"
                    }
                }
            };
        }

        var currentTags = NormalizeTags(current.Product.Tags);
        var currentMetafields = NormalizeMetafields(current.Metafields)
            .ToDictionary(
                metafield => BuildMetafieldKey(metafield.Namespace, metafield.Key),
                metafield => metafield,
                StringComparer.OrdinalIgnoreCase);

        var desiredBarcode = NormalizeBarcode(desired.Variant.Barcode);
        var currentBarcode = NormalizeBarcode(current.Variant.Barcode);
        var barcodeUpdate = desiredBarcode is not null && !string.Equals(desiredBarcode, currentBarcode, StringComparison.Ordinal)
            ? desiredBarcode
            : null;

        var tagsToAdd = desiredTags
            .Where(tag => !currentTags.Contains(tag, StringComparer.Ordinal))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();

        var metafieldsToUpsert = desiredMetafields
            .Where(desiredMetafield =>
            {
                var key = BuildMetafieldKey(desiredMetafield.Namespace, desiredMetafield.Key);
                return !currentMetafields.TryGetValue(key, out var existing) ||
                       !string.Equals(existing.Type, desiredMetafield.Type, StringComparison.Ordinal) ||
                       !string.Equals(existing.Value, desiredMetafield.Value, StringComparison.Ordinal);
            })
            .ToList();

        var ignored = new List<FlowEngineAkeneoSendToShopifyDifference>();
        AppendIgnoredDifference(ignored, "product.title", desired.Product.Title, current.Product.Title);
        AppendIgnoredDifference(ignored, "product.handle", desired.Product.Handle, current.Product.Handle);
        AppendIgnoredDifference(ignored, "product.productType", desired.Product.ProductType, current.Product.ProductType);
        AppendIgnoredDifference(ignored, "product.descriptionHtml", desired.Product.DescriptionHtml, current.Product.DescriptionHtml);
        AppendIgnoredDifference(ignored, "product.status", desired.Product.Status, current.Product.Status);

        var desiredImages = string.Join(",", desired.Product.ImageUrls.OrderBy(value => value, StringComparer.Ordinal));
        var currentImages = string.Join(",", current.Product.ImageUrls.OrderBy(value => value, StringComparer.Ordinal));
        if (!string.Equals(desiredImages, currentImages, StringComparison.Ordinal))
        {
            ignored.Add(new FlowEngineAkeneoSendToShopifyDifference
            {
                Field = "product.images",
                Desired = string.IsNullOrWhiteSpace(desiredImages) ? null : desiredImages,
                Current = string.IsNullOrWhiteSpace(currentImages) ? null : currentImages
            });
        }

        if (!string.Equals(Normalize(desired.Product.Vendor)?.ToLowerInvariant(), Normalize(current.Product.Vendor)?.ToLowerInvariant(), StringComparison.Ordinal))
        {
            ignored.Add(new FlowEngineAkeneoSendToShopifyDifference
            {
                Field = "product.vendor",
                Desired = Normalize(desired.Product.Vendor),
                Current = Normalize(current.Product.Vendor)
            });
        }

        AppendIgnoredDifference(ignored, "variant.price", desired.Variant.Price, current.Variant.Price);
        AppendIgnoredDifference(ignored, "variant.compareAtPrice", desired.Variant.CompareAtPrice, current.Variant.CompareAtPrice);
        ignored = ignored
            .OrderBy(value => value.Field, StringComparer.Ordinal)
            .ThenBy(value => value.Desired ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        var warnings = new List<FlowEngineAkeneoSendToShopifyWarning>();
        if (HasLargeDescriptionChange(desired.Product.DescriptionHtml, current.Product.DescriptionHtml))
        {
            warnings.Add(new FlowEngineAkeneoSendToShopifyWarning
            {
                Code = WarningDescriptionLargeChange,
                Message = "Description differs significantly and is currently ignored"
            });
        }

        var updateDraft = new FlowEngineAkeneoShopifyUpdateDraft
        {
            VariantBarcode = barcodeUpdate,
            TagsToAdd = tagsToAdd,
            MetafieldsToUpsert = NormalizeMetafields(metafieldsToUpsert)
        };

        return new FlowEngineAkeneoSendToShopifyUpdatePlan
        {
            WouldCreate = false,
            NoChange = !HasChanges(updateDraft),
            WouldUpdate = updateDraft,
            IgnoredDifferences = ignored,
            Warnings = warnings
        };
    }

    internal static List<string> NormalizeTags(IEnumerable<string> rawTags)
    {
        return rawTags
            .Select(value => Normalize(value)?.ToLowerInvariant())
            .Where(value => value is not null)
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
    }

    internal static List<FlowEngineAkeneoShopifyMetafieldDraft> NormalizeMetafields(
        IEnumerable<FlowEngineAkeneoShopifyMetafieldDraft> metafields)
    {
        return metafields
            .GroupBy(metafield => BuildMetafieldKey(metafield.Namespace, metafield.Key), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .OrderBy(metafield => BuildMetafieldKey(metafield.Namespace, metafield.Key), StringComparer.Ordinal)
            .ToList();
    }

    private static string BuildMetafieldKey(string? nameSpace, string? key)
        => $"{Normalize(nameSpace)?.ToLowerInvariant()}.{Normalize(key)?.ToLowerInvariant()}";

    private static List<string> SplitCsv(string? rawValue)
    {
        return string.IsNullOrWhiteSpace(rawValue)
            ? new List<string>()
            : rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Normalize)
                .Where(value => value is not null)
                .Select(value => value!)
                .ToList();
    }

    internal static string? PreferredTitle(AkeneoProduct product)
    {
        if (Normalize(product.WebBeskr) is { } web)
            return web;

        var baseTitle = Normalize(product.ArtBeskr);
        var specTitle = Normalize(product.ArtBeskrSpec);
        return (baseTitle, specTitle) switch
        {
            ({ } baseValue, { } specValue) => $"{baseValue}, {specValue}",
            ({ } baseValue, null) => baseValue,
            (null, { } specValue) => specValue,
            _ => null
        };
    }

    private static string? BuildHandle(string? title)
    {
        if (Normalize(title) is not { } normalizedTitle)
            return null;

        var decomposed = normalizedTitle.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var previousWasSeparator = false;

        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        var handle = builder.ToString().Trim('-');
        while (handle.Contains("--", StringComparison.Ordinal))
            handle = handle.Replace("--", "-", StringComparison.Ordinal);

        return string.IsNullOrWhiteSpace(handle) ? null : handle;
    }

    private static string BuildSourceHash(AkeneoProduct product)
    {
        var selected = string.Join("|", new[]
        {
            Normalize(product.ArtNr) ?? string.Empty,
            Normalize(product.Identifier) ?? string.Empty,
            Normalize(product.ShopifySync) ?? string.Empty,
            Normalize(product.ArtNrEan) ?? string.Empty,
            Normalize(product.VaruGruppKod) ?? string.Empty,
            Normalize(product.Directive) ?? string.Empty,
            Normalize(product.MainImage) ?? string.Empty,
            Normalize(product.WebBeskr) ?? string.Empty,
            Normalize(product.ArtBeskr) ?? string.Empty,
            Normalize(product.ArtBeskrSpec) ?? string.Empty,
            Normalize(product.DescriptionLong) ?? string.Empty
        });

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(selected));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static List<FlowEngineAkeneoShopifyMetafieldDraft> BuildAkeneoMetafields(AkeneoProduct product)
    {
        return new List<FlowEngineAkeneoShopifyMetafieldDraft>
        {
            new() { Namespace = "akeneo", Key = "artnr", Type = "single_line_text_field", Value = Normalize(product.ArtNr) ?? string.Empty },
            new() { Namespace = "akeneo", Key = "shopify_sync", Type = "single_line_text_field", Value = Normalize(product.ShopifySync) ?? string.Empty },
            new() { Namespace = "akeneo", Key = "ean", Type = "single_line_text_field", Value = Normalize(product.ArtNrEan) ?? string.Empty },
            new() { Namespace = "akeneo", Key = "varugruppkod", Type = "single_line_text_field", Value = Normalize(product.VaruGruppKod) ?? string.Empty },
            new() { Namespace = "akeneo", Key = "directives", Type = "multi_line_text_field", Value = Normalize(product.Directive) ?? string.Empty },
            new() { Namespace = "akeneo", Key = "image_tokens", Type = "multi_line_text_field", Value = Normalize(product.MainImage) ?? string.Empty },
            new() { Namespace = "akeneo", Key = "source_hash", Type = "single_line_text_field", Value = BuildSourceHash(product) }
        };
    }

    internal static string? Normalize(string? value)
    {
        if (value is null)
            return null;

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string? NormalizeBarcode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    private static bool IsValidGtin(string? value)
    {
        var digits = NormalizeBarcode(value);
        if (digits is null || digits.Length is not (8 or 12 or 13 or 14))
            return false;

        var numbers = digits.Select(character => character - '0').ToArray();
        var checkDigit = numbers[^1];
        var payload = numbers[..^1].Reverse().ToArray();
        var sum = 0;
        for (var index = 0; index < payload.Length; index++)
        {
            var weight = index % 2 == 0 ? 3 : 1;
            sum += payload[index] * weight;
        }

        var expected = (10 - (sum % 10)) % 10;
        return expected == checkDigit;
    }

    private static void AppendIgnoredDifference(
        ICollection<FlowEngineAkeneoSendToShopifyDifference> output,
        string field,
        string? desired,
        string? current)
    {
        var normalizedDesired = Normalize(desired);
        var normalizedCurrent = Normalize(current);
        if (string.Equals(normalizedDesired, normalizedCurrent, StringComparison.Ordinal))
            return;

        output.Add(new FlowEngineAkeneoSendToShopifyDifference
        {
            Field = field,
            Desired = normalizedDesired,
            Current = normalizedCurrent
        });
    }

    private static bool HasLargeDescriptionChange(string? desired, string? current)
    {
        var desiredValue = Normalize(desired) ?? string.Empty;
        var currentValue = Normalize(current) ?? string.Empty;
        if (string.Equals(desiredValue, currentValue, StringComparison.Ordinal))
            return false;

        var maxLength = Math.Max(desiredValue.Length, currentValue.Length);
        if (maxLength < 300)
            return false;

        var delta = Math.Abs(desiredValue.Length - currentValue.Length);
        return (double)delta / maxLength >= 0.35d;
    }
}

internal sealed record SyncDecision(string Status, string? Code, string? Message);

internal sealed record DesiredDraftResult(
    FlowEngineAkeneoShopifyDraft Draft,
    IReadOnlyList<FlowEngineAkeneoSendToShopifyWarning> Warnings);

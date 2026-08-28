using System.Text.Json;
using System.Text.Json.Serialization;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.Services.Integration.Akeneo;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineAkeneoSendToShopifyService : IFlowEngineAkeneoSendToShopifyService
{
    private const int DefaultSelectionLimit = 100;

    private const string CodeMissingRequiredScope = "SHP-PROD-VAL-005";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly IAkeneoClient _akeneoClient;
    private readonly IFlowEngineShopifyConnectionService _shopifyConnectionService;
    private readonly IFlowEngineShopifyScopeProbeService _shopifyScopeProbeService;
    private readonly IFlowEngineShopifyGraphQlClient _shopifyGraphQlClient;

    public FlowEngineAkeneoSendToShopifyService(
        IAkeneoClient akeneoClient,
        IFlowEngineShopifyConnectionService shopifyConnectionService,
        IFlowEngineShopifyScopeProbeService shopifyScopeProbeService,
        IFlowEngineShopifyGraphQlClient shopifyGraphQlClient)
    {
        _akeneoClient = akeneoClient;
        _shopifyConnectionService = shopifyConnectionService;
        _shopifyScopeProbeService = shopifyScopeProbeService;
        _shopifyGraphQlClient = shopifyGraphQlClient;
    }

    public async Task<FlowEngineOperationExecutionData> ExecuteAsync(
        JeevesRuntimeContext runtimeContext,
        FlowEngineExecuteJobRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Operation != FlowEngineOperationType.AkeneoSendToShopify)
            throw new InvalidOperationException($"Operationen {request.Operation} stods inte av Akeneo send-to-shopify-tjansten.");

        if (!request.Flags.DryRun)
            throw new InvalidOperationException("Akeneo send-to-shopify stoder bara dry run i den har parityfasen.");

        var selectedSkus = request.Params.AkeneoSkus
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var effectiveLimit = request.Params.UseLimit && request.Params.Limit.HasValue && request.Params.Limit.Value > 0
            ? request.Params.Limit.Value
            : DefaultSelectionLimit;

        IReadOnlyList<AkeneoProduct> products = selectedSkus.Count > 0
            ? await _akeneoClient.FetchProductsBySkusAsync(selectedSkus, effectiveLimit, cancellationToken)
            : await _akeneoClient.FetchProductsAsync(effectiveLimit, cancellationToken);

        var selectedProducts = products
            .OrderBy(product => FlowEngineAkeneoShopifySyncHelper.Normalize(product.Identifier) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(product => FlowEngineAkeneoShopifySyncHelper.Normalize(product.ArtNr) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedProducts.Count == 0)
            throw new InvalidOperationException("Akeneo send-to-shopify hittade inga produkter for valt urval.");

        var shopifyConnection = await _shopifyConnectionService.CreateAsync(runtimeContext.CompanyId, cancellationToken);
        var storeDomain = shopifyConnection.StoreDomain;
        var endpointUrl = shopifyConnection.EndpointUrl;
        var accessToken = shopifyConnection.AccessToken;
        var grantedScopes = (await _shopifyScopeProbeService.ResolveGrantedScopesAsync(endpointUrl, accessToken, cancellationToken)).Scopes;

        if (!grantedScopes.Contains("read_products"))
            throw new InvalidOperationException($"Shopify saknar read_products for Akeneo send-to-shopify ({CodeMissingRequiredScope}).");

        var payload = new FlowEngineAkeneoSendToShopifyPayload
        {
            DryRun = true,
            Scope = selectedSkus.Count > 0 ? "skus" : "all",
            RequestedSkus = selectedSkus,
            GrantedScopes = grantedScopes.OrderBy(value => value, StringComparer.Ordinal).ToList()
        };

        foreach (var product in selectedProducts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            payload.Counts.Requested += 1;
            var sku = FlowEngineAkeneoShopifySyncHelper.Normalize(product.Identifier) ?? $"missing-sku-{payload.Counts.Requested}";
            var title = FlowEngineAkeneoShopifySyncHelper.PreferredTitle(product);
            var decision = FlowEngineAkeneoShopifySyncHelper.BuildDecision(product, title);

            switch (decision.Status)
            {
                case FlowEngineAkeneoShopifySyncHelper.StatusEligible:
                    payload.Counts.Eligible += 1;
                    break;
                case FlowEngineAkeneoShopifySyncHelper.StatusSkipped:
                    payload.Counts.Skipped += 1;
                    break;
                default:
                    payload.Counts.Failed += 1;
                    break;
            }

            var item = new FlowEngineAkeneoSendToShopifyItem
            {
                Sku = sku,
                DecisionStatus = decision.Status,
                DecisionCode = decision.Code,
                DecisionMessage = decision.Message
            };

            if (decision.Status == FlowEngineAkeneoShopifySyncHelper.StatusEligible)
            {
                var desiredResult = FlowEngineAkeneoShopifySyncHelper.BuildDesiredDraft(product);
                item.Desired = desiredResult.Draft;
                item.Warnings.AddRange(desiredResult.Warnings.OrderBy(warning => warning.Code, StringComparer.Ordinal));

                try
                {
                    item.Current = await FetchCurrentShopifyDraftBySkuAsync(endpointUrl, accessToken, desiredResult.Draft.Sku, cancellationToken);
                    item.UpdatePlan = FlowEngineAkeneoShopifySyncHelper.Diff(desiredResult.Draft, item.Current);
                    item.Warnings.AddRange(
                        item.UpdatePlan.Warnings
                            .Where(warning => item.Warnings.All(existing => existing.Code != warning.Code || existing.Message != warning.Message))
                            .OrderBy(warning => warning.Code, StringComparer.Ordinal));
                }
                catch (Exception ex)
                {
                    item.RuntimeError = ex.Message;
                    payload.Counts.Failed += 1;
                }

                item.WouldCreate = item.UpdatePlan.WouldCreate;
                item.WouldUpdate = FlowEngineAkeneoShopifySyncHelper.HasChanges(item.UpdatePlan.WouldUpdate);
                item.NoChange = item.UpdatePlan.NoChange;

                if (item.WouldCreate)
                    payload.Counts.WouldCreate += 1;
                if (item.WouldUpdate)
                    payload.Counts.WouldUpdate += 1;
                if (item.NoChange)
                    payload.Counts.NoChange += 1;
            }

            payload.Items.Add(item);
        }

        payload.Counts.Total = payload.Counts.Requested;
        payload.Items = payload.Items
            .OrderBy(item => item.Sku, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new FlowEngineOperationExecutionData
        {
            SummaryLines =
            {
                $"Akeneo send-to-shopify dry run: Requested={payload.Counts.Requested}, Eligible={payload.Counts.Eligible}, Skipped={payload.Counts.Skipped}, Failed={payload.Counts.Failed}, WouldCreate={payload.Counts.WouldCreate}, WouldUpdate={payload.Counts.WouldUpdate}, NoChange={payload.Counts.NoChange}",
                $"Portalbolag: {runtimeContext.CompanyName} ({runtimeContext.CompanyCode})",
                $"Selection: {(selectedSkus.Count > 0 ? $"SKU-filter ({selectedSkus.Count})" : $"all (limit {effectiveLimit})")}",
                $"Shopify store: {storeDomain}"
            },
            JsonOutput = JsonSerializer.Serialize(payload, JsonOptions)
        };
    }

    private async Task<FlowEngineAkeneoShopifyDraft?> FetchCurrentShopifyDraftBySkuAsync(
        Uri endpointUrl,
        string accessToken,
        string sku,
        CancellationToken cancellationToken)
    {
        var response = await _shopifyGraphQlClient.PostAsync<ShopifyLookupData>(
            endpointUrl,
            accessToken,
            ShopifyLookupProductBySkuQuery,
            new Dictionary<string, object?> { ["query"] = $"sku:{sku}" },
            "AkeneoShopifyLookupProductBySKU",
            cancellationToken);

        var node = response.Products?.Edges?.FirstOrDefault()?.Node;
        if (node is null)
            return null;

        var variant = node.Variants?.Edges?
            .Select(edge => edge.Node)
            .FirstOrDefault(candidate => string.Equals(FlowEngineAkeneoShopifySyncHelper.Normalize(candidate?.Sku), FlowEngineAkeneoShopifySyncHelper.Normalize(sku), StringComparison.OrdinalIgnoreCase))
            ?? node.Variants?.Edges?.FirstOrDefault()?.Node;

        return new FlowEngineAkeneoShopifyDraft
        {
            Sku = FlowEngineAkeneoShopifySyncHelper.Normalize(variant?.Sku) ?? sku,
            Product = new FlowEngineAkeneoShopifyProductDraft
            {
                Title = FlowEngineAkeneoShopifySyncHelper.Normalize(node.Title),
                Handle = FlowEngineAkeneoShopifySyncHelper.Normalize(node.Handle),
                Vendor = FlowEngineAkeneoShopifySyncHelper.Normalize(node.Vendor),
                ProductType = FlowEngineAkeneoShopifySyncHelper.Normalize(node.ProductType),
                Status = FlowEngineAkeneoShopifySyncHelper.Normalize(node.Status),
                Tags = FlowEngineAkeneoShopifySyncHelper.NormalizeTags(node.Tags ?? new List<string>()),
                DescriptionHtml = FlowEngineAkeneoShopifySyncHelper.Normalize(node.DescriptionHtml),
                ImageTokens = new List<string>(),
                ImageUrls = (node.Images?.Edges ?? new List<ShopifyImageEdge>())
                    .Select(edge => FlowEngineAkeneoShopifySyncHelper.Normalize(edge.Node?.Url))
                    .Where(value => value is not null)
                    .Select(value => value!)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToList()
            },
            Variant = new FlowEngineAkeneoShopifyVariantDraft
            {
                Sku = FlowEngineAkeneoShopifySyncHelper.Normalize(variant?.Sku) ?? sku,
                Barcode = FlowEngineAkeneoShopifySyncHelper.Normalize(variant?.Barcode),
                Price = FlowEngineAkeneoShopifySyncHelper.Normalize(variant?.Price),
                CompareAtPrice = FlowEngineAkeneoShopifySyncHelper.Normalize(variant?.CompareAtPrice)
            },
            Metafields = FlowEngineAkeneoShopifySyncHelper.NormalizeMetafields(
                (node.Metafields?.Edges ?? new List<ShopifyMetafieldEdge>())
                    .Select(edge => edge.Node)
                    .Where(value => FlowEngineAkeneoShopifySyncHelper.Normalize(value?.Namespace) is not null && FlowEngineAkeneoShopifySyncHelper.Normalize(value?.Key) is not null)
                    .Select(value => new FlowEngineAkeneoShopifyMetafieldDraft
                    {
                        Namespace = FlowEngineAkeneoShopifySyncHelper.Normalize(value!.Namespace)!,
                        Key = FlowEngineAkeneoShopifySyncHelper.Normalize(value.Key)!,
                        Type = FlowEngineAkeneoShopifySyncHelper.Normalize(value.Type) ?? "single_line_text_field",
                        Value = FlowEngineAkeneoShopifySyncHelper.Normalize(value.Value) ?? string.Empty
                    })
                    .ToList())
        };
    }

    private sealed class ShopifyLookupData
    {
        public ShopifyProductsConnection? Products { get; set; }
    }

    private sealed class ShopifyProductsConnection
    {
        public List<ShopifyProductEdge>? Edges { get; set; }
    }

    private sealed class ShopifyProductEdge
    {
        public ShopifyProductNode? Node { get; set; }
    }

    private sealed class ShopifyProductNode
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Handle { get; set; }
        public string? Vendor { get; set; }
        public string? ProductType { get; set; }
        public string? Status { get; set; }
        public string? DescriptionHtml { get; set; }
        public List<string>? Tags { get; set; }
        public ShopifyImagesConnection? Images { get; set; }
        public ShopifyVariantsConnection? Variants { get; set; }
        public ShopifyMetafieldsConnection? Metafields { get; set; }
    }

    private sealed class ShopifyImagesConnection
    {
        public List<ShopifyImageEdge>? Edges { get; set; }
    }

    private sealed class ShopifyImageEdge
    {
        public ShopifyImageNode? Node { get; set; }
    }

    private sealed class ShopifyImageNode
    {
        public string? Url { get; set; }
        public string? AltText { get; set; }
    }

    private sealed class ShopifyVariantsConnection
    {
        public List<ShopifyVariantEdge>? Edges { get; set; }
    }

    private sealed class ShopifyVariantEdge
    {
        public ShopifyVariantNode? Node { get; set; }
    }

    private sealed class ShopifyVariantNode
    {
        public string? Id { get; set; }
        public string? Sku { get; set; }
        public string? Barcode { get; set; }
        public string? Price { get; set; }
        public string? CompareAtPrice { get; set; }
    }

    private sealed class ShopifyMetafieldsConnection
    {
        public List<ShopifyMetafieldEdge>? Edges { get; set; }
    }

    private sealed class ShopifyMetafieldEdge
    {
        public ShopifyMetafieldNode? Node { get; set; }
    }

    private sealed class ShopifyMetafieldNode
    {
        public string? Namespace { get; set; }
        public string? Key { get; set; }
        public string? Type { get; set; }
        public string? Value { get; set; }
    }

    private const string ShopifyLookupProductBySkuQuery = """
                                                           query AkeneoShopifyLookupProductBySKU($query: String!) {
                                                             products(first: 1, query: $query, sortKey: UPDATED_AT, reverse: false) {
                                                               edges {
                                                                 node {
                                                                   id
                                                                   title
                                                                   handle
                                                                   vendor
                                                                   productType
                                                                   status
                                                                   descriptionHtml
                                                                   tags
                                                                   images(first: 50) {
                                                                     edges {
                                                                       node {
                                                                         url
                                                                         altText
                                                                       }
                                                                     }
                                                                   }
                                                                   variants(first: 50) {
                                                                     edges {
                                                                       node {
                                                                         id
                                                                         sku
                                                                         barcode
                                                                         price
                                                                         compareAtPrice
                                                                       }
                                                                     }
                                                                   }
                                                                   metafields(first: 100, namespace: "akeneo") {
                                                                     edges {
                                                                       node {
                                                                         namespace
                                                                         key
                                                                         type
                                                                         value
                                                                       }
                                                                     }
                                                                   }
                                                                 }
                                                               }
                                                             }
                                                           }
                                                           """;
}

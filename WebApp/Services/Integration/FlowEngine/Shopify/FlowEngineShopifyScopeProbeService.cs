using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineShopifyScopeProbeService : IFlowEngineShopifyScopeProbeService
{
    private readonly IFlowEngineShopifyGraphQlClient _shopifyGraphQlClient;
    private readonly IFlowEngineShopifyQueryCatalog _shopifyQueryCatalog;

    public FlowEngineShopifyScopeProbeService(
        IFlowEngineShopifyGraphQlClient shopifyGraphQlClient,
        IFlowEngineShopifyQueryCatalog shopifyQueryCatalog)
    {
        _shopifyGraphQlClient = shopifyGraphQlClient;
        _shopifyQueryCatalog = shopifyQueryCatalog;
    }

    public async Task<FlowEngineShopifyScopeProbeResult> ResolveGrantedScopesWithShopDetailsAsync(
        Uri endpointUrl,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var response = await _shopifyGraphQlClient.PostAsync<ShopifyScopesCheckData>(
            endpointUrl,
            accessToken,
            _shopifyQueryCatalog.ScopesCheckQuery,
            new Dictionary<string, object?>(),
            "ShopifyScopesCheck",
            cancellationToken);

        return new FlowEngineShopifyScopeProbeResult(
            response.CurrentAppInstallation?.AccessScopes?
                .Select(scope => Normalize(scope.Handle)?.ToLowerInvariant())
                .Where(scope => scope is not null)
                .Select(scope => scope!)
                .ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal),
            response.Shop?.Name,
            response.Shop?.MyshopifyDomain);
    }

    public async Task<FlowEngineShopifyScopeProbeResult> ResolveGrantedScopesAsync(
        Uri endpointUrl,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var response = await _shopifyGraphQlClient.PostAsync<ShopifyCurrentAccessScopesData>(
            endpointUrl,
            accessToken,
            _shopifyQueryCatalog.CurrentAccessScopesQuery,
            new Dictionary<string, object?>(),
            "ShopifyCurrentAccessScopes",
            cancellationToken);

        return new FlowEngineShopifyScopeProbeResult(
            response.CurrentAppInstallation?.AccessScopes?
                .Select(scope => Normalize(scope.Handle)?.ToLowerInvariant())
                .Where(scope => scope is not null)
                .Select(scope => scope!)
                .ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal),
            null,
            null);
    }

    public FlowEngineShopifyScopeProbeCategory[] BuildCategories(HashSet<string> grantedScopes)
        => new[]
        {
            EvaluateScopeCategory("products", "get-products", grantedScopes),
            EvaluateScopeCategory("fetch", "fetch-orders", grantedScopes),
            EvaluateScopeCategory("validate", "validate-orders", grantedScopes),
            EvaluateScopeCategory("send", "send-orders", grantedScopes),
            EvaluateScopeCategory("check", "check-orders", grantedScopes),
            EvaluateScopeCategory("complete", "complete-orders", grantedScopes)
        };

    public void ValidateRequiredScopes(string subcommand, HashSet<string> grantedScopes)
    {
        var category = EvaluateScopeCategory(subcommand, subcommand, grantedScopes);
        if (!category.IsSatisfied)
        {
            var parts = new List<string>();
            if (category.MissingRequiredScopes.Length > 0)
                parts.Add($"Missing scopes: {string.Join(",", category.MissingRequiredScopes)}");
            if (category.MissingAnyOfScopes.Length > 0)
                parts.Add($"Missing one fulfillment scope set: {string.Join(",", category.MissingAnyOfScopes)}");

            throw new InvalidOperationException($"Shopify scopes invalid for {subcommand}: {string.Join("; ", parts)}");
        }
    }

    private static FlowEngineShopifyScopeProbeCategory EvaluateScopeCategory(string category, string subcommand, HashSet<string> grantedScopes)
    {
        var requiredAll = subcommand switch
        {
            "get-products" => new[] { "read_products" },
            "fetch-orders" or "fetch-order" or "validate-orders" or "validate-order" or "send-orders" or "send-order" or "check-orders" or "complete-orders" or "complete-order" or "complete-orders-pending"
                => new[] { "read_orders" },
            _ => Array.Empty<string>()
        };

        string[][] anyOf = subcommand switch
        {
            "complete-orders" or "complete-order" or "complete-orders-pending" => new[]
            {
                new[] { "read_merchant_managed_fulfillment_orders", "write_merchant_managed_fulfillment_orders" },
                new[] { "read_assigned_fulfillment_orders", "write_assigned_fulfillment_orders" }
            },
            _ => Array.Empty<string[]>()
        };

        var missingRequired = requiredAll.Where(scope => !grantedScopes.Contains(scope)).OrderBy(scope => scope, StringComparer.Ordinal).ToArray();
        var anyOfSatisfied = anyOf.Length == 0 || anyOf.Any(set => set.All(scope => grantedScopes.Contains(scope)));
        var missingAnyOf = anyOf.Length == 0
            ? Array.Empty<string>()
            : anyOf.Select(set => set.Where(scope => !grantedScopes.Contains(scope)).OrderBy(scope => scope, StringComparer.Ordinal).ToArray())
                .OrderBy(set => set.Length)
                .FirstOrDefault() ?? Array.Empty<string>();

        return new FlowEngineShopifyScopeProbeCategory(category, missingRequired.Length == 0 && anyOfSatisfied, missingRequired, missingAnyOf);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class ShopifyScopesCheckData
    {
        public ShopifyShopData? Shop { get; set; }
        public ShopifyCurrentAppInstallation? CurrentAppInstallation { get; set; }
    }

    private sealed class ShopifyCurrentAccessScopesData
    {
        public ShopifyCurrentAppInstallation? CurrentAppInstallation { get; set; }
    }

    private sealed class ShopifyShopData
    {
        public string? Name { get; set; }
        public string? MyshopifyDomain { get; set; }
    }

    private sealed class ShopifyCurrentAppInstallation
    {
        public List<ShopifyAccessScope>? AccessScopes { get; set; }
    }

    private sealed class ShopifyAccessScope
    {
        public string? Handle { get; set; }
    }
}

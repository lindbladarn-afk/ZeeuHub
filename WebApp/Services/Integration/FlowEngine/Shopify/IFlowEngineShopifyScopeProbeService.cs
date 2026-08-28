using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineShopifyScopeProbeService
{
    Task<FlowEngineShopifyScopeProbeResult> ResolveGrantedScopesWithShopDetailsAsync(
        Uri endpointUrl,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<FlowEngineShopifyScopeProbeResult> ResolveGrantedScopesAsync(
        Uri endpointUrl,
        string accessToken,
        CancellationToken cancellationToken = default);

    FlowEngineShopifyScopeProbeCategory[] BuildCategories(HashSet<string> grantedScopes);

    void ValidateRequiredScopes(string subcommand, HashSet<string> grantedScopes);
}

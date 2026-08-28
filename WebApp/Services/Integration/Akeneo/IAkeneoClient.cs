using WebApp.Models.Integration;

namespace WebApp.Services.Integration.Akeneo
{
    public interface IAkeneoClient
    {
        Task<IReadOnlyList<AkeneoProduct>> FetchProductsAsync(int limit, CancellationToken ct = default);
        Task<IReadOnlyList<AkeneoProduct>> FetchProductsBySkusAsync(IReadOnlyList<string> skus, int limit, CancellationToken ct = default);
    }
}

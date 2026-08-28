using System.Threading;
using System.Threading.Tasks;

namespace WebApp.Services.Integration
{
    public interface IJeevesAuthService
    {
        Task<string?> GetAccessTokenAsync(string cacheKey, string authUrl, string appId, string appSecret, CancellationToken ct = default);
        void Invalidate(string cacheKey);
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebApp.Repositories.Jeeves;

namespace WebApp.Services.Application
{
    public sealed class PersSignService : IPersSignService
    {
        private readonly IJeevesUserRepository _repo;

        public PersSignService(IJeevesUserRepository repo)
        {
            _repo = repo;
        }

        public async Task<IReadOnlyList<string>> GetAvailablePersSignsAsync(
            Guid connectionStringId,
            CancellationToken ct = default)
        {
            var cs = ResolveConnectionString(connectionStringId);
            if (string.IsNullOrWhiteSpace(cs))
                return Array.Empty<string>();

            return await _repo.GetPersSignsAsync(cs, ct);
        }

        public async Task<bool> PersSignExistsAsync(
            Guid connectionStringId,
            string persSign,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(persSign))
                return false;

            var cs = ResolveConnectionString(connectionStringId);
            if (string.IsNullOrWhiteSpace(cs))
                return false;

            return await _repo.PersSignExistsAsync(cs, persSign.Trim(), ct);
        }

        private static string? ResolveConnectionString(Guid connectionStringId)
        {
            var envKey = $"CONNECTION_STRING_{connectionStringId:N}".ToUpperInvariant();
            return Environment.GetEnvironmentVariable(envKey);
        }
    }
}
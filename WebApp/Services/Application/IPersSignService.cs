using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WebApp.Services.Application
{
    public interface IPersSignService
    {
        Task<IReadOnlyList<string>> GetAvailablePersSignsAsync(
            Guid connectionStringId,
            CancellationToken ct = default);

        Task<bool> PersSignExistsAsync(
            Guid connectionStringId,
            string persSign,
            CancellationToken ct = default);
    }
}
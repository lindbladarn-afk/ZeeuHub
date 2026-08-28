using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WebApp.Repositories.Jeeves
{
    public interface IJeevesUserRepository
    {
        Task<IReadOnlyList<string>> GetPersSignsAsync(string jeevesConnectionString, CancellationToken ct = default);
        Task<bool> PersSignExistsAsync(string jeevesConnectionString, string persSign, CancellationToken ct = default);
    }
}
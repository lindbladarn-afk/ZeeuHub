using System;
using System.Threading;
using System.Threading.Tasks;

namespace WebApp.Services.Application
{
    public interface IUserWhitelistService
    {
        Task<bool> IsWhitelistedAsync(string? email, string? userId, Guid? companyId, CancellationToken ct = default);
    }
}

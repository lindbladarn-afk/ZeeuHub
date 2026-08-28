using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebApp.Data;

namespace WebApp.Services.Application
{
    public class UserWhitelistService : IUserWhitelistService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
        private readonly ILogger<UserWhitelistService> _logger;

        public UserWhitelistService(IDbContextFactory<ApplicationDbContext> dbContextFactory, ILogger<UserWhitelistService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        public async Task<bool> IsWhitelistedAsync(string? email, string? userId, Guid? companyId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(userId))
                return false;

            var normalizedEmail = email?.Trim().ToLowerInvariant();
            try
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

                var query = db.UserWhitelists!
                    .AsNoTracking()
                    .Where(x => x.IsActive);

                if (companyId.HasValue)
                    query = query.Where(x => x.CompanyId == null || x.CompanyId == companyId);
                else
                    query = query.Where(x => x.CompanyId == null);

                return await query.AnyAsync(x =>
                        (!string.IsNullOrWhiteSpace(normalizedEmail) &&
                         x.Email != null &&
                         x.Email.ToLower() == normalizedEmail) ||
                        (!string.IsNullOrWhiteSpace(userId) &&
                         x.UserId != null &&
                         x.UserId == userId),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Whitelist lookup failed.");
                return false;
            }
        }
    }
}

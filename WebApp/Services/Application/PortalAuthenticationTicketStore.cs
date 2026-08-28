using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Application;

namespace WebApp.Services.Application;

// Store auth tickets server-side so the browser cookie stays tiny and old cookies are less likely to break requests.
public sealed class PortalAuthenticationTicketStore : ITicketStore
{
    private static readonly TicketSerializer TicketSerializer = TicketSerializer.Default;

    private readonly IDataProtector _protector;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<PortalAuthenticationTicketStore> _logger;

    public PortalAuthenticationTicketStore(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<PortalAuthenticationTicketStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _protector = dataProtectionProvider.CreateProtector("PortalAuthenticationTicketStore.v1");
        _logger = logger;
    }

    public Task<string> StoreAsync(AuthenticationTicket ticket)
        => StoreAsync(ticket, CancellationToken.None);

    public async Task<string> StoreAsync(AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var key = $"auth-{Guid.NewGuid():N}";
        await UpsertAsync(key, ticket, cancellationToken);
        return key;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
        => RenewAsync(key, ticket, CancellationToken.None);

    public async Task RenewAsync(string key, AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(ticket);

        await UpsertAsync(key, ticket, cancellationToken);
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key)
        => RetrieveAsync(key, CancellationToken.None);

    public async Task<AuthenticationTicket?> RetrieveAsync(string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await context.Set<PortalAuthenticationTicketRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == key, cancellationToken);

        if (record == null)
            return null;

        if (record.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            _logger.LogInformation("Removing expired authentication ticket {TicketKey}", key);
            await RemoveAsync(key, cancellationToken);
            return null;
        }

        try
        {
            var unprotectedPayload = _protector.Unprotect(record.Payload);
            return TicketSerializer.Deserialize(unprotectedPayload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize authentication ticket {TicketKey}. Removing stored ticket.", key);
            await RemoveAsync(key, cancellationToken);
            return null;
        }
    }

    public Task RemoveAsync(string key)
        => RemoveAsync(key, CancellationToken.None);

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await context.Set<PortalAuthenticationTicketRecord>()
            .FirstOrDefaultAsync(x => x.Id == key, cancellationToken);

        if (record == null)
            return;

        context.Remove(record);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertAsync(string key, AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var nowUtc = DateTime.UtcNow;
        var expiresAtUtc = ResolveExpirationUtc(ticket);
        var userId = ticket.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? ticket.Principal?.FindFirstValue(IdentityOptionsClaims.UserIdClaimType);
        var payload = _protector.Protect(TicketSerializer.Serialize(ticket));

        var record = await context.Set<PortalAuthenticationTicketRecord>()
            .FirstOrDefaultAsync(x => x.Id == key, cancellationToken);

        if (record == null)
        {
            record = new PortalAuthenticationTicketRecord
            {
                Id = key,
                CreatedAtUtc = nowUtc
            };
            context.Add(record);
        }

        record.UserId = userId;
        record.Payload = payload;
        record.ExpiresAtUtc = expiresAtUtc;
        record.UpdatedAtUtc = nowUtc;

        await context.SaveChangesAsync(cancellationToken);
    }

    private static DateTimeOffset ResolveExpirationUtc(AuthenticationTicket ticket)
    {
        if (ticket.Properties.ExpiresUtc.HasValue)
            return ticket.Properties.ExpiresUtc.Value;

        if (ticket.Properties.IssuedUtc.HasValue)
            return ticket.Properties.IssuedUtc.Value.AddHours(12);

        return DateTimeOffset.UtcNow.AddHours(12);
    }

    private static class IdentityOptionsClaims
    {
        public const string UserIdClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
    }
}

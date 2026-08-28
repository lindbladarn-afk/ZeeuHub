using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Data;
using WebApp.Services.Application;

namespace WebApp.Tests;

public sealed class PortalAuthenticationTicketStoreTests
{
    [Fact]
    public async Task StoreAsync_Encrypts_Payload_And_RetrieveAsync_Restores_Ticket()
    {
        var factory = new TestDbContextFactory(Guid.NewGuid().ToString("N"));
        var provider = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        var store = new PortalAuthenticationTicketStore(factory, provider, NullLogger<PortalAuthenticationTicketStore>.Instance);

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-123"),
                new Claim(ClaimTypes.Name, "Alice")
            },
            "Cookies");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
        }, "Cookies");

        var key = await store.StoreAsync(ticket);

        await using (var db = await factory.CreateDbContextAsync())
        {
            var record = await db.Set<WebApp.Models.Application.PortalAuthenticationTicketRecord>().SingleAsync(item => item.Id == key);
            var rawText = System.Text.Encoding.UTF8.GetString(record.Payload);

            Assert.DoesNotContain("user-123", rawText);
            Assert.DoesNotContain("Alice", rawText);
        }

        var restored = await store.RetrieveAsync(key);

        Assert.NotNull(restored);
        Assert.Equal("user-123", restored!.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("Alice", restored.Principal.Identity?.Name);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public TestDbContextFactory(string dbName)
        {
            _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
        }

        public ApplicationDbContext CreateDbContext()
            => new(_options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}

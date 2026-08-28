using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Security.Claims;
using WebApp.Services.Purchase.Demo;
using WebApp.Services;

namespace WebApp.Tests;

public sealed class PurchaseDemoModeServiceTests
{
    [Fact]
    public void SetEnabled_PersistsState_PerCompany()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature());
        httpContext.User = CreatePrincipal("Administrator");
        httpContext.Session.Set("UserObject", new UserSession
        {
            CompanyId = Guid.NewGuid()
        });

        var service = new PurchaseDemoModeService(new HttpContextAccessor { HttpContext = httpContext });

        Assert.False(service.IsEnabled());

        service.SetEnabled(true);

        Assert.True(service.IsEnabled());

        service.SetEnabled(false);

        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void SetEnabled_Ignores_NonAdminUsers()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature());
        httpContext.User = CreatePrincipal("User");
        httpContext.Session.Set("UserObject", new UserSession
        {
            CompanyId = Guid.NewGuid()
        });

        var service = new PurchaseDemoModeService(new HttpContextAccessor { HttpContext = httpContext });

        service.SetEnabled(true);

        Assert.False(service.IsEnabled());
    }

    private static ClaimsPrincipal CreatePrincipal(string role)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, role)
        }, "TestAuth");

        return new ClaimsPrincipal(identity);
    }

    private sealed class TestSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new TestSession();
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);

        public IEnumerable<string> Keys => _values.Keys;
        public bool IsAvailable => true;
        public string Id => "test";

        public void Clear() => _values.Clear();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Remove(string key) => _values.Remove(key);

        public void Set(string key, byte[] value) => _values[key] = value;

        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }
}

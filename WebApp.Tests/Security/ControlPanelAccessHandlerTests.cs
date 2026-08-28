using System.Security.Claims;
using Entities.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using WebApp.Models.ControlPanel;
using WebApp.Services;
using WebApp.Services.ControlPanel;

namespace WebApp.Tests;

// Verifies the Control Panel policy handler delegates to the tenant rule service.
public sealed class ControlPanelAccessHandlerTests
{
    [Fact]
    public async Task HandleAsync_Succeeds_For_Authorized_Tenant()
    {
        var httpContext = CreateHttpContext(new UserSession
        {
            UserId = "user-1",
            CompanyName = "  ZeeU AB  "
        });
        var handler = CreateHandler(httpContext);
        var authContext = CreateAuthorizationContext();

        await handler.HandleAsync(authContext);

        Assert.True(authContext.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_Does_Not_Succeed_For_Unexpected_Tenant()
    {
        var httpContext = CreateHttpContext(new UserSession
        {
            UserId = "user-1",
            CompanyName = "Other AB"
        });
        var handler = CreateHandler(httpContext);
        var authContext = CreateAuthorizationContext();

        await handler.HandleAsync(authContext);

        Assert.False(authContext.HasSucceeded);
    }

    private static ControlPanelAccessHandler CreateHandler(DefaultHttpContext httpContext)
    {
        return new ControlPanelAccessHandler(
            new HttpContextAccessor { HttpContext = httpContext },
            new ControlPanelAccessService(Options.Create(new ControlPanelOptions
            {
                AllowedCompanyName = "ZeeU AB"
            })));
    }

    private static AuthorizationHandlerContext CreateAuthorizationContext()
    {
        var requirement = new ControlPanelAccessRequirement();
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        return new AuthorizationHandlerContext(new[] { requirement }, principal, resource: null);
    }

    private static DefaultHttpContext CreateHttpContext(UserSession sessionUser)
    {
        var context = new DefaultHttpContext();
        context.Features.Set<ISessionFeature>(new TestSessionFeature());
        context.Session.Set("UserObject", sessionUser);
        return context;
    }

    private sealed class TestSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new TestSession();
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new();

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _values.Keys;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Clear() => _values.Clear();
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }
}

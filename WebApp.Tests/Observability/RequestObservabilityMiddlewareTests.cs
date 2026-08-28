using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Middleware;
using WebApp.Observability;

namespace WebApp.Tests;

// Verifies the request identifiers exposed to support and downstream telemetry.
public sealed class RequestObservabilityMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_CreatesSupportAndCorrelationHeaders()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        using var activity = new Activity("request").Start();
        await middleware.InvokeAsync(context);

        Assert.False(string.IsNullOrWhiteSpace(context.Response.Headers[PortalObservability.CorrelationHeaderName]));
        Assert.False(string.IsNullOrWhiteSpace(context.Response.Headers[PortalObservability.SupportHeaderName]));
        Assert.Equal(8, context.Response.Headers[PortalObservability.SupportHeaderName].ToString().Length);
    }

    [Fact]
    public async Task InvokeAsync_ReusesSafeIncomingCorrelationHeader()
    {
        const string correlationId = "customer-request-42";
        var context = CreateContext();
        context.Request.Headers[PortalObservability.CorrelationHeaderName] = correlationId;
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal(correlationId, context.Response.Headers[PortalObservability.CorrelationHeaderName]);
        Assert.Equal(correlationId, context.Items[PortalObservability.CorrelationIdItemKey]);
    }

    [Fact]
    public async Task InvokeAsync_RejectsUnsafeIncomingCorrelationHeader()
    {
        var context = CreateContext();
        context.Request.Headers[PortalObservability.CorrelationHeaderName] = "unsafe value\r\nInjected: true";
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var generated = context.Response.Headers[PortalObservability.CorrelationHeaderName].ToString();
        Assert.Matches("^[a-f0-9]{32}$", generated);
    }

    private static RequestObservabilityMiddleware CreateMiddleware(RequestDelegate next)
        => new(next, NullLogger<RequestObservabilityMiddleware>.Instance, new TestHostEnvironment());

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<ISessionFeature>(new TestSessionFeature { Session = new TestSession() });
        return context;
    }

    private sealed class TestSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = null!;
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _values.Keys;

        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "WebApp.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

// Verifies that explicit tenant data-source selection cannot fall back across tenant boundaries.
using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using WebApp.Helpers;
using WebApp.Services;
using WebApp.Services.Application;
using WebApp.Services.Application.AI;

namespace WebApp.Tests;

public sealed class AiDataSourceResolverTests
{
    [Fact]
    public async Task ResolveAsync_ExplicitTenantWithoutConnection_DoesNotFallbackToExternal()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:DataSources:0:Key"] = "tenant",
                ["Ai:DataSources:0:Name"] = "Aktiv tenant",
                ["Ai:DataSources:0:IsTenantConnection"] = "true",
                ["Ai:DataSources:0:IsDefault"] = "true",
                ["Ai:DataSources:1:Key"] = "external",
                ["Ai:DataSources:1:Name"] = "External",
                ["Ai:DataSources:1:ConnectionString"] =
                    "Server=external.example;Database=ExternalDb;User Id=user;Password=password;TrustServerCertificate=True"
            })
            .Build();
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature());
        httpContext.Session.Set("UserObject", new UserSession { UserId = "user-1" });
        var resolver = new AiDataSourceResolver(
            configuration,
            new HttpContextAccessor { HttpContext = httpContext },
            new StubRuntimeContextService(connectionString: string.Empty));

        var (connectionString, info) = await resolver.ResolveAsync("tenant");

        Assert.Empty(connectionString);
        Assert.Equal("tenant", info.Key);
        Assert.True(info.IsTenantConnection);
    }

    private sealed class StubRuntimeContextService : IJeevesRuntimeContextService
    {
        private readonly string _connectionString;

        public StubRuntimeContextService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public Task<OperationResult<JeevesRuntimeContext>> ResolveAsync(
            UserSession? sessionUser,
            CancellationToken cancellationToken = default)
        {
            var context = new JeevesRuntimeContext
            {
                UserId = sessionUser?.UserId ?? string.Empty,
                ConnectionString = _connectionString
            };
            return Task.FromResult(new OperationResult<JeevesRuntimeContext>(true, context));
        }
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

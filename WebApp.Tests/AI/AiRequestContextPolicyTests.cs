// Verifies that AI execution context cannot be overridden by client-controlled values.
using System.Text.Json;
using WebApp.Models.AI;
using WebApp.Services.Application;
using WebApp.Services.Application.AI;

namespace WebApp.Tests;

public sealed class AiRequestContextPolicyTests
{
    [Fact]
    public void Apply_NonAdministrator_OverwritesClientContextAndForcesTenant()
    {
        var resolver = new StubDataSourceResolver(
            DataSource("tenant", isTenant: true),
            DataSource("external", isTenant: false));
        var policy = new AiRequestContextPolicy(resolver);
        var request = new AiQueryRequest
        {
            CompanyCode = 999,
            RuntimeConnectionString = "client-connection",
            DataSourceKey = "external"
        };

        var result = policy.Apply(
            request,
            RuntimeContext(companyCode: 100, connectionString: "server-connection"),
            isAdministrator: false,
            requireTenantDataSource: false);

        Assert.True(result.Success);
        Assert.Equal(100, request.CompanyCode);
        Assert.Equal("server-connection", request.RuntimeConnectionString);
        Assert.Equal("tenant", request.DataSourceKey);
    }

    [Fact]
    public void Apply_Administrator_AllowsConfiguredExternalDataSource()
    {
        var resolver = new StubDataSourceResolver(
            DataSource("tenant", isTenant: true),
            DataSource("external", isTenant: false));
        var policy = new AiRequestContextPolicy(resolver);
        var request = new AiQueryRequest { DataSourceKey = "EXTERNAL" };

        var result = policy.Apply(
            request,
            RuntimeContext(),
            isAdministrator: true,
            requireTenantDataSource: false);

        Assert.True(result.Success);
        Assert.Equal("external", request.DataSourceKey);
    }

    [Fact]
    public void Apply_Administrator_RejectsUnknownDataSource()
    {
        var resolver = new StubDataSourceResolver(DataSource("tenant", isTenant: true));
        var policy = new AiRequestContextPolicy(resolver);
        var request = new AiQueryRequest { DataSourceKey = "unknown" };

        var result = policy.Apply(
            request,
            RuntimeContext(),
            isAdministrator: true,
            requireTenantDataSource: false);

        Assert.False(result.Success);
        Assert.Equal("Den valda AI-datakällan är inte tillåten.", result.Error);
    }

    [Fact]
    public void Apply_AssistantSource_ForcesTenantForAdministrator()
    {
        var resolver = new StubDataSourceResolver(
            DataSource("tenant", isTenant: true),
            DataSource("external", isTenant: false));
        var policy = new AiRequestContextPolicy(resolver);
        var request = new AiQueryRequest { DataSourceKey = "external" };

        var result = policy.Apply(
            request,
            RuntimeContext(),
            isAdministrator: true,
            requireTenantDataSource: true);

        Assert.True(result.Success);
        Assert.Equal("tenant", request.DataSourceKey);
    }

    [Fact]
    public void Apply_NonAdministratorWithoutTenantDataSource_IsDenied()
    {
        var resolver = new StubDataSourceResolver(DataSource("external", isTenant: false));
        var policy = new AiRequestContextPolicy(resolver);
        var request = new AiQueryRequest { DataSourceKey = "external" };

        var result = policy.Apply(
            request,
            RuntimeContext(),
            isAdministrator: false,
            requireTenantDataSource: false);

        Assert.False(result.Success);
        Assert.Contains("tenant", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_IgnoresClientOwnedCompanyAndConnectionValues()
    {
        const string json = """
            {
              "question": "Visa öppna fakturor",
              "companyCode": 999,
              "runtimeConnectionString": "client-connection",
              "dataSourceKey": "tenant"
            }
            """;

        var request = JsonSerializer.Deserialize<AiQueryRequest>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(request);
        Assert.Null(request.CompanyCode);
        Assert.Null(request.RuntimeConnectionString);
        Assert.Equal("tenant", request.DataSourceKey);
    }

    private static AiDataSourceInfo DataSource(string key, bool isTenant) => new()
    {
        Key = key,
        Name = key,
        IsTenantConnection = isTenant,
        HasConnectionString = true
    };

    private static JeevesRuntimeContext RuntimeContext(
        int companyCode = 100,
        string connectionString = "server-connection") => new()
    {
        CompanyCode = companyCode,
        ConnectionString = connectionString
    };

    private sealed class StubDataSourceResolver : IAiDataSourceResolver
    {
        private readonly IReadOnlyList<AiDataSourceInfo> _dataSources;

        public StubDataSourceResolver(params AiDataSourceInfo[] dataSources)
        {
            _dataSources = dataSources;
        }

        public IReadOnlyList<AiDataSourceInfo> GetConfiguredDataSources() => _dataSources;

        public Task<(string ConnectionString, AiDataSourceInfo Info)> ResolveAsync(
            string? requestedKey = null,
            CancellationToken ct = default)
        {
            var selected = _dataSources.First();
            return Task.FromResult(("connection", selected));
        }

        public void SetSelected(string key)
        {
        }

        public string? GetSelected() => null;
    }
}

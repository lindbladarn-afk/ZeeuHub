using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Observability;

namespace WebApp.Tests;

// Verifies that local startup does not require an Application Insights connection.
public sealed class ObservabilityRegistrationTests
{
    [Fact]
    public void AddPortalObservability_AllowsMissingAzureMonitorConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();
        var services = new ServiceCollection();

        var result = services.AddPortalObservability(configuration);

        Assert.Same(services, result);
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider);
    }
}

using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Trace;
using Repository.Execution;

namespace WebApp.Observability;

// Registers Azure Monitor telemetry only when an export connection is configured.
public static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddPortalObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["AzureMonitor:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        }

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services
                .AddOpenTelemetry()
                .UseAzureMonitor(options => options.ConnectionString = connectionString)
                .WithTracing(tracing => tracing.AddSource(
                    PortalObservability.ActivitySourceName,
                    JeevesSqlTelemetry.ActivitySourceName));
        }

        services.AddExceptionHandler<PortalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }
}

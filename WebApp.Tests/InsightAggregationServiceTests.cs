// Tests Action Center aggregation behavior around provider failures.
using Entities.Application;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Models.ActionCenter;
using WebApp.Services.ActionCenter;
using WebApp.Services.Application;

namespace WebApp.Tests;

// Verifies that Action Center provider failures stay useful without exposing internal exception details.
public sealed class InsightAggregationServiceTests
{
    [Fact]
    public async Task GetInsightsAsync_Sanitizes_Provider_Exception_Message()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new InsightAggregationService(
            cache,
            new IInsightProvider[] { new ThrowingInsightProvider() },
            NullLogger<InsightAggregationService>.Instance);

        var result = await service.GetInsightsAsync(
            new UserSession { UserId = "user-1" },
            ActionCenterAudience.Customer,
            "action-center-test",
            "ActionCenter",
            runtimeContext: null,
            CancellationToken.None);

        var failure = Assert.Single(result.Failures);
        Assert.Equal("customer-throwing", failure.ProviderKey);
        Assert.Equal("Insikten kunde inte laddas just nu.", failure.Message);
    }

    private sealed class ThrowingInsightProvider : IInsightProvider
    {
        public string ProviderKey => "customer-throwing";
        public ActionCenterAudience Audience => ActionCenterAudience.Customer;

        public Task<IEnumerable<ActionCenterInsight>> GetInsightsAsync(
            UserSession user,
            JeevesRuntimeContext? runtimeContext,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("Server=prod;Password=secret;");
    }
}

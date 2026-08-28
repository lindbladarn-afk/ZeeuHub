using WebApp.Models.Dashboard;
using WebApp.Services.Orders;

namespace WebApp.Tests;

public sealed class OrdersAnalyticsModelBuilderTests
{
    [Fact]
    public void BuildRevenueModel_KeepsFullAnnualRunRateOrderBasisForScrollableDetails()
    {
        var builder = new OrdersAnalyticsModelBuilder();
        var orders = Enumerable.Range(1, 12)
            .Select(index => new OrderTotalPoint
            {
                OrderNumber = index,
                OrderNumberText = $"SO-{index:000}",
                OrderDate = new DateTime(2026, 4, 1).AddDays(index),
                AmountInclVat = 1000m + index
            })
            .ToArray();

        var model = builder.BuildRevenueModel(orders, Array.Empty<TopSellerItem>(), usesFallbackPeriod: false);

        Assert.Equal(12, model.AnnualRunRateDetails.OrdersCount);
        Assert.Equal(12, model.AnnualRunRateDetails.TopOrders.Count);
        Assert.Equal("SO-012", model.AnnualRunRateDetails.TopOrders.First().OrderLabel);
    }
}

using Microsoft.Extensions.FileProviders;
using WebApp.Services.Purchase.Demo;

namespace WebApp.Tests;

public sealed class PurchaseDemoDataServiceTests
{
    [Fact]
    public async Task LoadAsync_ReadsBundledPurchaseDemoOrders()
    {
        var webAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));
        var environment = new TestHostEnvironment
        {
            ContentRootPath = webAppRoot,
            ContentRootFileProvider = new PhysicalFileProvider(webAppRoot)
        };
        var service = new PurchaseDemoDataService(environment);

        var result = await service.LoadAsync();

        Assert.NotEmpty(result.Orders);
        Assert.Contains(result.Orders, x => x.OrderNumber == 12345);
        Assert.Contains(result.Orders, x => x.SupplierName == "Best");
    }

    [Fact]
    public async Task FindOrderAsync_ReturnsMatchingPurchaseOrder()
    {
        var webAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));
        var environment = new TestHostEnvironment
        {
            ContentRootPath = webAppRoot,
            ContentRootFileProvider = new PhysicalFileProvider(webAppRoot)
        };
        var service = new PurchaseDemoDataService(environment);

        var result = await service.FindOrderAsync(900081);

        Assert.NotNull(result);
        Assert.Equal(900081, result?.OrderNumber);
        Assert.Equal("Länsförsäkringar Liv", result?.SupplierName);
    }
}

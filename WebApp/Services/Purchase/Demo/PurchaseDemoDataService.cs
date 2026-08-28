using System.Text.Json;
using Entities.Purchase;
using Microsoft.AspNetCore.Hosting;
using WebApp.Models.Purchase.Demo;

namespace WebApp.Services.Purchase.Demo;

// Reads the static purchase demo file that backs the Azure showcase mode.
public sealed class PurchaseDemoDataService : IPurchaseDemoDataService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IWebHostEnvironment _environment;

    public PurchaseDemoDataService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<PurchaseDemoData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = ResolveDemoFilePath();
        var orders = await ReadAsync<List<PurchaseOrderVM>>(path, cancellationToken) ?? new List<PurchaseOrderVM>();

        return new PurchaseDemoData
        {
            Orders = orders
        };
    }

    public async Task<PurchaseOrderVM?> FindOrderAsync(int orderNumber, CancellationToken cancellationToken = default)
    {
        var data = await LoadAsync(cancellationToken);
        return data.Orders.FirstOrDefault(order => order.OrderNumber == orderNumber);
    }

    private string ResolveDemoFilePath()
        => Path.Combine(_environment.ContentRootPath, "Data", "Purchase", "demo", "purchase-orders.json");

    private static async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return default;

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }
}

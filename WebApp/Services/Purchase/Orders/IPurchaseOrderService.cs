using Entities.Purchase;

namespace WebApp.Services.Purchase.Orders;

// Coordinates Purchase order reads and commands against the active Jeeves context.
public interface IPurchaseOrderService
{
    Task<IEnumerable<IPurchaseOrderVM>> GetMyPurchaseOrdersAsync(CancellationToken cancellationToken = default);
    Task<IPurchaseOrderVM> GetPurchaseOrderAsync(int orderNumber, CancellationToken cancellationToken = default);
    Task<PurchaseOrderVM> CreateEmptyPurchaseOrderAsync(CancellationToken cancellationToken = default);
    Task<PurchaseOrderCommandResult> CreatePurchaseOrderAsync(PurchaseOrderVM purchaseOrder, CancellationToken cancellationToken = default);
    Task<IPurchaseOrderResultDto> CreateStockDeliveryAsync(PurchaseOrderVM purchaseOrder, CancellationToken cancellationToken = default);
}

public sealed class PurchaseOrderCommandResult
{
    public bool Success { get; init; }
    public bool ValidationFailed { get; init; }
    public string? Message { get; init; }
    public int? OrderNumber { get; init; }

    public static PurchaseOrderCommandResult FromValidationError(string message)
        => new() { ValidationFailed = true, Message = message };

    public static PurchaseOrderCommandResult FromRepositoryResult(IPurchaseOrderResultDto result)
        => new()
        {
            Success = result.Success,
            Message = result.Message,
            OrderNumber = result.OrderNumber
        };
}

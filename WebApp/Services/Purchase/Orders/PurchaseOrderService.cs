using Entities.Purchase;
using Repository.Contracts;
using WebApp.Services.Purchase.Demo;

using WebApp.Services.Purchase.Context;

namespace WebApp.Services.Purchase.Orders;

// Keeps Purchase order workflow rules and repository calls out of the MVC controller.
public sealed class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IPurchaseContextService _contextService;
    private readonly IPurchaseDemoDataService _purchaseDemoDataService;
    private readonly IPurchaseDemoModeService _purchaseDemoModeService;
    private readonly IPurchaseRepository _purchaseRepository;

    public PurchaseOrderService(
        IPurchaseContextService contextService,
        IPurchaseDemoDataService purchaseDemoDataService,
        IPurchaseDemoModeService purchaseDemoModeService,
        IPurchaseRepository purchaseRepository)
    {
        _contextService = contextService;
        _purchaseDemoDataService = purchaseDemoDataService;
        _purchaseDemoModeService = purchaseDemoModeService;
        _purchaseRepository = purchaseRepository;
    }

    public async Task<IEnumerable<IPurchaseOrderVM>> GetMyPurchaseOrdersAsync(CancellationToken cancellationToken = default)
    {
        if (_purchaseDemoModeService.IsEnabled())
            return (await _purchaseDemoDataService.LoadAsync(cancellationToken)).Orders;

        var context = await _contextService.BuildAsync(cancellationToken);
        return await _purchaseRepository.GetMyPurchaseOrdersAsync(context.ConnectionString, context.CompanyCode, context.PersSign);
    }

    public async Task<IPurchaseOrderVM> GetPurchaseOrderAsync(int orderNumber, CancellationToken cancellationToken = default)
    {
        if (_purchaseDemoModeService.IsEnabled())
        {
            var demoOrder = await _purchaseDemoDataService.FindOrderAsync(orderNumber, cancellationToken);
            if (demoOrder is null)
                throw new InvalidOperationException($"Demo purchase order {orderNumber} was not found");

            return demoOrder;
        }

        var context = await _contextService.BuildAsync(cancellationToken);
        return await _purchaseRepository.GetPurchaseOrderAsync(
            context.ConnectionString,
            context.CompanyCode,
            context.PersSign,
            orderNumber);
    }

    public async Task<PurchaseOrderVM> CreateEmptyPurchaseOrderAsync(CancellationToken cancellationToken = default)
    {
        await _contextService.BuildAsync(cancellationToken);

        return new PurchaseOrderVM
        {
            OrderRows = new List<PurchaseOrderRowVM>
            {
                new()
            }
        };
    }

    public async Task<PurchaseOrderCommandResult> CreatePurchaseOrderAsync(
        PurchaseOrderVM purchaseOrder,
        CancellationToken cancellationToken = default)
    {
        var context = await _contextService.BuildAsync(cancellationToken);
        var invalidArticleNumber = FindInvalidArticleNumber(context, purchaseOrder);
        if (!string.IsNullOrWhiteSpace(invalidArticleNumber))
        {
            return PurchaseOrderCommandResult.FromValidationError(
                $"The article {invalidArticleNumber} is not flagged as an expence article in Jeeves");
        }

        var missingRequiredRowValue = FindMissingRequiredRowValue(purchaseOrder);
        if (!string.IsNullOrWhiteSpace(missingRequiredRowValue))
        {
            return PurchaseOrderCommandResult.FromValidationError(missingRequiredRowValue);
        }

        var result = _purchaseRepository.CreatePurchaseOrder(
            context.ConnectionString,
            context.PersSign,
            context.FullName,
            context.CompanyCode,
            purchaseOrder);

        return PurchaseOrderCommandResult.FromRepositoryResult(result);
    }

    public async Task<IPurchaseOrderResultDto> CreateStockDeliveryAsync(
        PurchaseOrderVM purchaseOrder,
        CancellationToken cancellationToken = default)
    {
        var context = await _contextService.BuildAsync(cancellationToken);
        return _purchaseRepository.CreateStockDelivery(
            context.ConnectionString,
            context.PersSign,
            context.CompanyCode,
            purchaseOrder);
    }

    private static string? FindInvalidArticleNumber(PurchaseRequestContext context, PurchaseOrderVM purchaseOrder)
    {
        var validArticleNumbers = context.Articles
            .Select(article => article.ArticleNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return purchaseOrder.OrderRows?
            .Select(row => row.ArticleNumber)
            .FirstOrDefault(articleNumber => !validArticleNumbers.Contains(articleNumber ?? string.Empty));
    }

    private static string? FindMissingRequiredRowValue(PurchaseOrderVM purchaseOrder)
    {
        var rows = purchaseOrder.OrderRows ?? new List<PurchaseOrderRowVM>();

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var rowNumber = index + 1;

            if (string.IsNullOrWhiteSpace(row.ArticleNumber))
                return $"Orderrad {rowNumber} saknar artikelnummer. Välj artikeln från listan innan du skapar beställningen.";

            if (string.IsNullOrWhiteSpace(row.Account))
                return $"Orderrad {rowNumber} saknar konto. Välj artikeln från listan så fylls konto från Jeeves.";

            if (string.IsNullOrWhiteSpace(row.CostCenter))
                return $"Orderrad {rowNumber} saknar kostnadsställe. Välj artikeln från listan så fylls kostnadsställe från Jeeves.";
        }

        return null;
    }
}

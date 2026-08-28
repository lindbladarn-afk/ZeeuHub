using Entities.Purchase;

namespace WebApp.Services.Purchase.Context;

// Builds the runtime data Purchase needs before it can read or write Jeeves orders.
public interface IPurchaseContextService
{
    Task<PurchaseRequestContext> BuildAsync(CancellationToken cancellationToken = default);
}

public sealed class PurchaseRequestContext
{
    public required string ConnectionString { get; init; }
    public int? CompanyCode { get; init; }
    public required string PersSign { get; init; }
    public required string FullName { get; init; }
    public required IReadOnlyList<IPurchaseOrderVM> Suppliers { get; init; }
    public required IReadOnlyList<IPurchaseArticleVM> Articles { get; init; }
    public required IReadOnlyList<IPurchaseSupplierContactVM> Contacts { get; init; }
}

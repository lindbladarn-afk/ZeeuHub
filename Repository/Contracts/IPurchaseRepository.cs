namespace Repository.Contracts;

public interface IPurchaseRepository
{
    Task<IEnumerable<IPurchaseOrderVM>> GetAllSuppliersAsync(string connectionString, int? companyCode);
    Task<IEnumerable<IPurchaseSuppliersAutoCompleteDto>> GetAutocompleteSuppliersAsync(string connectionString, int companyCode);
    Task<IEnumerable<IPurchaseSupplierContactVM>> GetAllContactsAsync(string connectionString, int? companyCode);
    Task<IEnumerable<IPurchaseArticleVM>> GetPurchaseArticlesAsync(string connectionString, int? companyCode);
    IPurchaseOrderResultDto CreateStockDelivery(string connectionString, string perssign, int? companyCode, IPurchaseOrderVM purchaseOrder);

    Task<IEnumerable<IPurchaseOrderVM>> GetMyPurchaseOrdersAsync(string connectionString, int? companyCode, string perssign);
    Task<IPurchaseOrderVM> GetPurchaseOrderAsync(string connectionString, int? companyCode, string perssign, int orderNumber);
    IPurchaseOrderResultDto CreatePurchaseOrder(string connectionString, string perssign, string? userFullName, int? companyCode, IPurchaseOrderVM purchaseOrder);
}

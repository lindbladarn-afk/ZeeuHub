namespace Entities.Purchase;

public interface IPurchaseSupplierContactVM
{
    string? ContactName { get; set; }
    string ContactNumber { get; set; }
    string? ContactNumberDescription { get; set; }
    string SupplierNumber { get; set; }
}
namespace Entities.Purchase;

public interface IPurchaseSuppliersAutoCompleteDto
{
    string? City { get; set; }
    string SupplierName { get; set; }
    string CompanyCode { get; set; }
    string? OrganizationNumber { get; set; }
}
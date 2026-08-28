namespace Entities.Purchase;

public interface IPurchaseOrderVM
{
    string SupplierNumber { get; set; }
    string SupplierName { get; set; }
    string? OrganizationNumber { get; set; }
    int? OrderNumber { get; set; }
    int? OrderStatusId { get; set; }
    string? Co { get; set; }
    string? Street { get; set; }
    string? ZipCode { get; set; }
    string? City { get; set; }
    string? Country { get; set; }
    string? CustomerNumberAtSupplier { get; set; }
    bool IsBlocked { get; set; }
    string Currency { get; set; }
    decimal OrderValue { get; set; }
    DateTime? RegisteredDate { get; set; }
    DateTime? DeliveryDate { get; set; }
    string? DeliveryCompany { get; set; }
    string? DeliveryCo { get; set; }
    string? DeliveryStreet { get; set; }
    string? DeliveryZip { get; set; }
    string? DeliveryCity { get; set; }
    string? DeliveryCountry { get; set; }


    int PurchaseOrderTypeId { get; set; }
    string PurchaseOrderType { get; set; }
    string? Message { get; set; }
    string? ContactNumber { get; set; }

    List<PurchaseOrderRowVM> OrderRows { get; set; }
    List<PurchaseSupplierContactVM> Contacts { get; set; }
}
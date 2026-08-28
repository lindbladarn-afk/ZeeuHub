namespace Entities.Purchase;

public interface IPurchaseOrderRowVM
{
    string ArticleDescription { get; set; }
    string ArticleNumber { get; set; }
    decimal Discount { get; set; }
    decimal Price { get; set; }
    decimal Quantity { get; set; }
    decimal RecievedQuantity { get; set; }
    DateTime? DeliveryDate { get; set; }
    DateTime? ConfirmedDeliveryDate { get; set; }
    string? Account { get; set; }
    string? CostCenter { get; set; }

    bool AddToStock { get; set; }
    bool IsRecieved { get; }
}

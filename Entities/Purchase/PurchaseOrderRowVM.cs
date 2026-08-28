namespace Entities.Purchase;

public class PurchaseOrderRowVM : IPurchaseOrderRowVM
{
    public string ArticleNumber { get; set; }
    public string ArticleDescription { get; set; }
    public decimal Quantity { get; set; }
    public decimal RecievedQuantity { get; set; }
    public int RowNumber { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public DateTime? ConfirmedDeliveryDate { get; set; }

    public string Unit { get; set; }
    public decimal Price { get; set; }
    public decimal Discount { get; set; }
    public string? Account { get; set; }
    public string? CostCenter { get; set; }



    public bool AddToStock { get; set; }

    public bool IsRecieved 
    { 
        get 
        {
            if (RecievedQuantity >= Quantity)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}

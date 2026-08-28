namespace Entities.Purchase;

public class PurchaseOrderResultDto : IPurchaseOrderResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int? OrderNumber { get; set; }
}

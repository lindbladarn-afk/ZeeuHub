namespace Entities.Purchase;

public class PurchaseOrderSqlResponseDto
{
    public bool Success { get; set; } = false;
    public string? Message { get; set; }
    public int? BestNr { get; set; }
}

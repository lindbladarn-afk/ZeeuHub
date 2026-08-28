namespace Entities.Purchase;

public class PurchaseOrderRowSqlResponseDto
{
    public bool Success { get; set; } = false;
    public string? Message { get; set; }
    public int? BestNr { get; set; }
    public int? BestRestNr { get; set; }
    public int? BestRadNr { get; set; }
    public string? DummyUniqueId { get; set; }
}

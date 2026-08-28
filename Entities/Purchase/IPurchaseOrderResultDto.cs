namespace Entities.Purchase;

public interface IPurchaseOrderResultDto
{
    string? Message { get; set; }
    int? OrderNumber { get; set; }
    bool Success { get; set; }
}
namespace WebApp.Services.Orders;

// Lightweight raw aggregate used internally before KPI/series mapping.
public sealed class OrderTotalPoint
{
    public long OrderNumber { get; set; }
    public string? OrderNumberText { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal AmountInclVat { get; set; }
}

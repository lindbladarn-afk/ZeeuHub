namespace Entities.ViewModels.WebApproval;

public class WebApprovalSaleOrderVM
{
    public Guid Id { get; set; }

    public string Foretagkod { get; set; }

    [Column(name:"OrderNr")]
    public long OrderNumber { get; set; }

    [Column(name: "ordstat")]
    public short? OrderStatus { get; set; }

    [Column(name: "OrderTyp")]
    public string OrderType { get; set; }

    [Column(name: "OrdStatBeskr")]
    public string? OrderStatusDescription { get; set; }

    public string SalesReference { get; set; }

    public string AttestantPersSign { get; set; }
    public string AttestantName { get; set; }

    [Column(name:"KundRef2")]
    public string? CustomerReference { get; set; }

    [Column(name: "FtgNr")]
    public string CustomerNumber { get; set; }

    [Column(name: "FtgNamn")]
    public string CustomerName { get; set; }

    [Column(name: "RegDat")]
    [DisplayFormat(DataFormatString = "{0:d}")] // Short date format based on location
    public DateTime OrderRegisteredDate { get; set; }

    [Column(name: "OrdBerLevDat")]
    [DisplayFormat(DataFormatString = "{0:d}")] // Short date format based on location
    public DateTime OrderEstimatedDeliveryDate { get; set; }

    [Column(name:"DelivAddr2")]
    public string? DeliveryAddress2 { get; set; }

    [Column(name: "DelivAddr3")]
    public string? DeliveryAddress3 { get; set; }

    [Column(name: "DelivAddr4")]
    public string? DeliveryAddress4 { get; set; }

    [Column(name: "ValKod")]
    public string Currency { get; set; }

    [Column(name: "VbOrdSum")]
    [DisplayFormat(DataFormatString = "{0:N2}")] // Will format like 1,000.00
    public decimal OrderValueLocal { get; set; }

    [Column(name: "OrdSum")]
    [DisplayFormat(DataFormatString = "{0:N2}")] // Will format like 1,000.00
    public decimal OrderValue { get; set; }

    public bool IsActive { get; set; }

    /// <summary>
    /// Null, 0 amd 3 is unhandled
    /// 1 = Approved
    /// 2 = Rejected
    /// </summary>
    [Column(name: "q_zu_approval_status")]
    public int ApprovalStatus { get; set; }

    [Column(name: "ApprovedBy")]
    public string? ApprovedBy { get; set; }

    [Column(name: "ApprovedDate")]
    public DateTime? ApprovedDate { get; set; }


    /// <summary>
    /// This is a q field in Xvivos environment
    /// </summary>
    public string? Xvivo_q_oh_anteckning { get; set; }
    public string? Message { get; set; }


    public List<WebApprovalSaleOrderRowVM> OrderRows { get; set; }
}

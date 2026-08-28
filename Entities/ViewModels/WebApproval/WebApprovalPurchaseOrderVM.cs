// Represents a purchase approval order shared by repositories and MVC views.
namespace Entities.ViewModels.WebApproval;

public class WebApprovalPurchaseOrderVM
{
    public Guid Id { get; set; }

    [Column(name: "BestNr")]
    public string OrderNumber { get; set; }

    [Column(name: "Vref")]
    public string Purchaser { get; set; }

    private string _attestantPersSign;
    public string AttestantPersSign 
    { 
        get { return _attestantPersSign.ToLower(); }
        set { _attestantPersSign = value; }  
    }
    public string AttestantName { get; set; }

    [Column(name:"FtgNr")]
    public string SupplierNumber { get; set; }

    [Column(name:"FtgNamn")]
    public string SupplierName { get; set; }

    [Column(name:"RegDat")]
    [DisplayFormat(DataFormatString = "{0:d}")] // Short date format based on location
    public DateTime OrderRegisteredDate { get; set; }

    [Column(name:"BestBerLevDat")]
    [DisplayFormat(DataFormatString = "{0:d}")] // Short date format based on location
    public DateTime OrderEstimatedDeliveryDate { get; set; }

    [Column(name:"ValKod")]
    public string Currency { get; set; }

    [Column(name: "VbBestValue")]
    [DisplayFormat(DataFormatString = "{0:N2}")] // Will format like 1,000.00
    public decimal OrderValueLocal { get; set; }
    
    [Column(name:"BestValue")]
    [DisplayFormat(DataFormatString = "{0:N2}")] // Will format like 1,000.00
    public decimal OrderValue { get; set; }

    [Column(name:"EditExt")]
    public string? EditExternal { get; set; }


    public bool IsActive { get; set; }

    public string? Message { get; set; }

	[Column(name: "ApprovedBy")]
	public string? ApprovedBy { get; set; }

	[Column(name: "ApprovedDate")]
	public DateTime? ApprovedDate { get; set; }

	public int ApprovalStatus { get; set; }

    //public Guid CompanyId { get; set; }
    public List<WebApprovalPurchaseOrderRowVM> OrderRows { get; set; }
}

namespace Entities.Purchase;

public class PurchaseOrderVM : IPurchaseOrderVM
{
    [Column(name: "FtgNr")]
    [Required(ErrorMessage = "Purchase_FtgNrRequired")]
    public string SupplierNumber { get; set; }

    [Column(name: "FtgNamn")]
    [Required(ErrorMessage = "Purchase_FtgNamnRequired")]
    //[Required(ErrorMessageResourceName = "Purchase_FtgNamnRequired",
    //    ErrorMessageResourceType = typeof(EntitiesResource))]
    public string SupplierName { get; set; }

    [Column(name: "OrgNr")]
    public string? OrganizationNumber { get; set; }

    [Column(name: "BestNr")]
    public int? OrderNumber { get; set; }

    [Column(name: "BestStatKod")]
    public int? OrderStatusId { get; set; }

    [Column(name: "FtgPostAdr1")]
    public string? Co { get; set; }

    [Column(name: "FtgPostAdr2")]
    public string? Street { get; set; }

    [Column(name: "FtgPostAdr3")]
    public string? City { get; set; }

    [Column(name: "FtgPostNr")]
    public string? ZipCode { get; set; }

    [Column(name: "Country")]
    public string? Country { get; set; }

    [Column(name: "KundNrHosLev")]
    public string? CustomerNumberAtSupplier { get; set; }

    [Column(name: "UtbSparr")]
    public bool IsBlocked { get; set; }

    [Column(name: "ValKod")]
    public string Currency { get; set; }

    [Column(name: "OrderValue")]
    public decimal OrderValue { get; set; }

    [Column(name:"RegDat")]
    public DateTime? RegisteredDate { get; set; }

    /// <summary>
    /// In the purchase order this is the requsted delivery date.
    /// </summary>
    [Column(name: "BestBegLevDat")]
    public DateTime? DeliveryDate { get; set; }

    [Column(name: "DeliveryFtgNamn")]
    public string? DeliveryCompany { get; set; }

    [Column(name:"DeliveryFtgPostAdr1")]
    public string? DeliveryCo { get; set; }

    [Column(name: "DeliveryFtgPostAdr2")]
    public string? DeliveryStreet { get; set; }

    [Column(name:"DeliveryFtgPostNr")]
    public string? DeliveryZip { get; set; }

    [Column(name:"DeliveryFtgPostAdr3")]
    public string? DeliveryCity { get; set; }

    [Column(name:"DeliveryLandsKod")]
    public string? DeliveryCountry { get; set; }


    public int PurchaseOrderTypeId { get; set; } = 900;
    public string PurchaseOrderType { get; set; } = "Expense";

    [Column(name:"EditExt")]
    public string? Message { get; set; }

    public string? ContactNumber { get; set; }


    public List<PurchaseOrderRowVM> OrderRows { get; set; }
    public List<PurchaseSupplierContactVM>? Contacts { get; set; }
}

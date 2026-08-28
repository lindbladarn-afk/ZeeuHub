namespace Entities.Purchase;

public class PurchaseSuppliersAutoCompleteDto : IPurchaseSuppliersAutoCompleteDto
{
    [Column(name: "ForetagKod")]
    public string CompanyCode { get; set; }

    [Column(name: "OrgNr")]
    public string? OrganizationNumber { get; set; }

    [Column(name:"FtgNr")]
    public string SupplierNumber { get; set; }

    [Column(name: "FtgNamn")]
    public string SupplierName { get; set; }

    [Column(name: "FtgPostAdr3")]
    public string? City { get; set; }

    [Column(name: "Country")]
    public string? Country { get; set; }

    [Column(name: "UtbSparr")]
    public bool IsBlocked { get; set; }
}

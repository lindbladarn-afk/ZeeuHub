namespace Entities.Customers;

public class CustomerViewModel : ICustomerViewModel
{
    [Column(name: "ForetagKod")]
    public string CustomerNumber { get; set; }

    [Column(name: "OrgNr")]
    public string? OrganizationNumber { get; set; }

    [Required]
    [Column(name: "FtgNamn")]
    public string CompanyName { get; set; }

    [Column(name:"SaljareNamn")]
    public string? SalesPerson { get; set; }

    [Column(name: "FtgPostAdr1")]
    public string? Co { get; set; }

    [Column(name: "FtgPostAdr2")]
    public string? Street { get; set; }

    [Column(name: "FtgPostAdr3")]
    public string? City { get; set; }
    
    [Column(name:"FtgPostNr")]
    public string? ZipCode { get; set; }

    [Column(name:"Country")]
    public string? Country { get; set; }

    [Column(name:"KundOmsFa")]
    [DisplayFormat(DataFormatString = "{0:N2}")] // Will format like 1,000.00
    public decimal? NetTurnoverLastYear { get; set; }

    [Column(name:"KundOms")]
    [DisplayFormat(DataFormatString = "{0:N2}")] // Will format like 1,000.00
    public decimal? NetTurnoverThisYear { get; set; }
}

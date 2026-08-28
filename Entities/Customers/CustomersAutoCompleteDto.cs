namespace Entities.Customers;

public class CustomersAutoCompleteDto : ICustomersAutoCompleteDto
{
    [Column(name: "ForetagKod")]
    public string CustomerNumber { get; set; }

    [Column(name: "OrgNr")]
    public string? OrganizationNumber { get; set; }

    [Column(name: "FtgNamn")]
    public string CompanyName { get; set; }

    [Column(name: "FtgPostAdr3")]
    public string? City { get; set; }
}

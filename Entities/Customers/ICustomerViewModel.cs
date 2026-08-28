namespace Entities.Customers;

public interface ICustomerViewModel
{
    string CustomerNumber { get; set; }
    string CompanyName { get; set; }
    string? OrganizationNumber { get; set; }
    string? SalesPerson { get; set; }
    string? Co { get; set; }
    string? Street { get; set; }
    string? ZipCode { get; set; }
    string? City { get; set; }
    string? Country { get; set; }
    decimal? NetTurnoverLastYear { get; set; }
    decimal? NetTurnoverThisYear { get; set; }
}
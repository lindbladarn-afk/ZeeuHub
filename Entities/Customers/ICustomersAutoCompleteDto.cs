namespace Entities.Customers;

public interface ICustomersAutoCompleteDto
{
    string? City { get; set; }
    string CompanyName { get; set; }
    string CustomerNumber { get; set; }
    string? OrganizationNumber { get; set; }
}
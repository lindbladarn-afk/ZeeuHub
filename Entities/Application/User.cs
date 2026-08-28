namespace Entities.Application;

public class User : IUser
{
	public string Id { get; set; }
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public string? PersSign { get; set; }
	public string? Language { get; set; }
	public Guid? CompanyId { get; set; }
	public byte[]? ProfilePicture { get; set; }
	public string? Email { get; set; }
	public string? PhoneNumber { get; set; }
    public int? JeevesActiveCompany { get; set; }
    public Guid? ActiveConnectionStringId { get; set; }

    public Company? Company { get; set; }

    public IEnumerable<JeevesCompanyVM>? JeevesCompanies { get; set; }
}

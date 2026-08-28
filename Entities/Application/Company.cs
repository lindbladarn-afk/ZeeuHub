namespace Entities.Application;

public class Company : ICompany
{
	public Guid Id { get; set; }
	public string Name { get; set; }
    public int? DefaultJeevesCompanyCode { get; set; }

    public List<CompanyLicense>? Licenses { get; set; }
	public List<CompanyPermission>? Permissions { get; set; }
    public List<CompanyConnectionString>? ConnectionStrings { get; set; }
}

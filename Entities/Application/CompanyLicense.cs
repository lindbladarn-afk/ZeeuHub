namespace Entities.Application;

public class CompanyLicense : ICompanyLicense
{
	public Guid Id { get; set; }
	public Guid CompanyId { get; set; }
	public Guid? ZeeuProductId { get; set; }
	public bool Enabled { get; set; }

	public ICompany? Company { get; set; }
	public IZeeuProduct? ZeeuProduct { get; set; }
}

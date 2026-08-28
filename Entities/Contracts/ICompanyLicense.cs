namespace Entities.Contracts;

public interface ICompanyLicense
{
	/// <summary>
	/// The Id assigned by the SQL on insert
	/// </summary>
	Guid Id { get; set; }

	/// <summary>
	/// Foreign Key to the Identity.Companies Id property
	/// </summary>
	Guid CompanyId { get; set; }

	/// <summary>
	/// Foreign Key to the Identity.ZeeUProducts Is property
	/// </summary>
	Guid? ZeeuProductId { get; set; }

	/// <summary>
	/// Flag that enables the licensed product in the portal
	/// </summary>
	bool Enabled { get; set; }


	ICompany? Company { get; set; }
	IZeeuProduct? ZeeuProduct { get; set; }
}
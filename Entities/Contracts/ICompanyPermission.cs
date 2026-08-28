namespace Entities.Contracts;

/// <summary>
/// The permissions a company has in the portal
/// This is used in the authorization
/// </summary>
public interface ICompanyPermission
{
	/// <summary>
	/// The Id assigned by the database on insert
	/// </summary>
	Guid Id { get; set; }

	/// <summary>
	/// Reference to the Identity.Company Id property
	/// </summary>
	Guid? CompanyId { get; set; }

	/// <summary>
	/// Reference to the Identity.Module Id property
	/// </summary>
	Guid? ModuleId { get; set; }

	/// <summary>
	/// Reference to the Identity.SubModule Id property
	/// </summary>
	Guid? SubModuleId { get; set; }



	Module? Module { get; set; }
	SubModule? SubModule { get; set; }
	//Company? Company { get; set; }
}
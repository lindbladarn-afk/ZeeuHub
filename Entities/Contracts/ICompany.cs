namespace Entities.Contracts;
 
public interface ICompany
{
	/// <summary>
	/// Id is a Guid set by the database when inserted
	/// </summary>
	Guid Id { get; set; }

	/// <summary>
	/// Company name
	/// </summary>
	string Name { get; set; }

	/// <summary>
	/// A list of licensed ZeeU products the company has access to
	/// </summary>
	List<CompanyLicense>? Licenses { get; set; }
	/// <summary>
	/// A collection of permissions the company has to modules in the portal
	/// </summary>
	List<CompanyPermission>? Permissions { get; set; }
}
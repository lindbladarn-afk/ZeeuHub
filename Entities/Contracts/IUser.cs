namespace Entities.Contracts;

public interface IUser
{
	/// <summary>
	/// The Id assigned by the database on insert
	/// </summary>
	string Id { get; }
	string? FirstName { get; set; }
	string? LastName { get; set; }

	/// <summary>
	/// The users PersSign in Jeeves
	/// </summary>
	string? PersSign { get; set; }

	/// <summary>
	/// The perferred language of the user
	/// </summary>
	string? Language { get; set; }

	/// <summary>
	/// Foreign Key to the Identity.Company Id property
	/// </summary>
	Guid? CompanyId { get; set; }

	byte[]? ProfilePicture { get; set; }

	string? Email { get; set; }

	string? PhoneNumber { get; set; }

	int? JeevesActiveCompany { get; set; }

        Guid? ActiveConnectionStringId { get; set; }

        Company? Company { get; set; }

	/// <summary>
	/// The companies connected to the PersSign in jeeves
	/// </summary>
	IEnumerable<JeevesCompanyVM>? JeevesCompanies { get; set; }
}

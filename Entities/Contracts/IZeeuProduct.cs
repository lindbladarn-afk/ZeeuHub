namespace Entities.Contracts;

public interface IZeeuProduct
{

	/// <summary>
	/// The Id assigned by the database on insert
	/// </summary>
	Guid Id { get; set; }

	/// <summary>
	/// The product name
	/// </summary>
	string? Name { get; set; }
	string? Description { get; set; }
	byte[]? Image { get; set; }
	decimal Price { get; set; }

	/// <summary>
	/// URL to an external web page
	/// </summary>
	string? Link { get; set; }

	/// <summary>
	/// Internal link to product within the application
	/// </summary>
	string? InternalLink { get; set; }


	List<ICompanyLicense>? Licenses { get; set; }
}
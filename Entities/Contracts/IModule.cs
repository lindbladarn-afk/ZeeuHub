namespace Entities.Contracts;

/// <summary>
/// Modules in the application (Corresponds to the controllers)
/// </summary>
public interface IModule
{
	/// <summary>
	/// Id assigned by the database on insert
	/// </summary>
	Guid Id { get; set; }

	/// <summary>
	/// The name of the Module (internal)
	/// </summary>
	string? Name { get; set; }

	/// <summary>
	/// Description of the section
	/// </summary>
	string? Description { get; set; }

	/// <summary>
	/// Controller the module points to
	/// </summary>
	string? MenuSectionController { get; set; }

	/// <summary>
	/// Action the module points to if there are no sub modules
	/// </summary>
	string? MenuSectionAction { get; set; }

	/// <summary>
	/// Icon that is displayed before the Section name
	/// Use the Font Awsome prefix for Solid or Regular
	/// E.g. fas fa-bell or far fa-bell
	/// </summary>
	string? MenuSectionIcon { get; set; }

	/// <summary>
	/// The text shown in the sidebar menu
	/// </summary>
	string? MenuSectionText { get; set; }

	/// <summary>
	/// Enable or disable the section for all users
	/// </summary>
	bool MenuSectionEnabled { get; set; }

	/// <summary>
	/// Sorting ordet in the menu tree
	/// </summary>
	int? MenuSectionSortOrder { get; set; }

	/// <summary>
	/// The ZeeUProduct Id if applicable
	/// </summary>
	Guid? ZeeuProductId { get; set; }


	List<ISubModule>? SubModules { get; set; }
	IZeeuProduct? ZeeuProduct { get; set; }
	List<ICompanyPermission>? Permissions { get; set; }
}
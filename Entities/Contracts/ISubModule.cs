namespace Entities.Contracts;

/// <summary>
/// Sub modules in the sidebar menu
/// </summary>
public interface ISubModule
{
	/// <summary>
	/// The Id assigned by the database on insert
	/// </summary>
	Guid Id { get; set; }

	/// <summary>
	/// The Id of the parent Module
	/// </summary>
	Guid ModuleId { get; set; }

	/// <summary>
	/// Internal name of the sub module
	/// </summary>
	string? Name { get; set; }

	string? Description { get; set; }

	/// <summary>
	/// The sub module text visible to the users
	/// </summary>
	string? MenuItemText { get; set; }

	/// <summary>
	/// The controller the SubModule is pointing to
	/// </summary>
	string? MenuItemController { get; set; }

	/// <summary>
	/// The action in the controller (ItemController)
	/// </summary>
	string? MenuItemAction { get; set; }

	/// <summary>
	/// Enable or Disable the sub module for all users
	/// </summary>
	bool? MenuItemEnabled { get; set; }

	/// <summary>
	/// Item sorting in the menu tree
	/// </summary>
	int? MenuItemSortOrder { get; set; }



	IModule? Module { get; set; }
	List<ICompanyPermission>? Permissions { get; set; }
}
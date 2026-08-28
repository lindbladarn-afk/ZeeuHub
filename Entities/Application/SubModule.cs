namespace Entities.Application;

public class SubModule : ISubModule
{
	public Guid Id { get; set; }
	public Guid ModuleId { get; set; }
	public string? Name { get; set; }
	public string? Description { get; set; }
	public string? MenuItemController { get; set; }
	public string? MenuItemAction { get; set; }
	public string? MenuItemText { get; set; }
	public bool? MenuItemEnabled { get; set; }
    public int? MenuItemSortOrder { get; set; }

    public IModule? Module { get; set; }
	public List<ICompanyPermission>? Permissions { get; set; }
}

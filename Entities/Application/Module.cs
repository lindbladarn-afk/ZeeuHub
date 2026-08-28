namespace Entities.Application;

public class Module : IModule
{
	public Guid Id { get; set; }
	public string? Name { get; set; }
	public string? Description { get; set; }
	public string? MenuSectionController { get; set; }
	public string? MenuSectionAction { get; set; }
	public string? MenuSectionIcon { get; set; }
	public string? MenuSectionText { get; set; }
	public bool MenuSectionEnabled { get; set; }
    public int? MenuSectionSortOrder{ get; set; }
    public Guid? ZeeuProductId { get; set; }


	public List<ISubModule>? SubModules { get; set; }
	public IZeeuProduct? ZeeuProduct { get; set; }
	public List<ICompanyPermission>? Permissions { get; set; }
}

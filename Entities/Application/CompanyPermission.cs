namespace Entities.Application;

public class CompanyPermission : ICompanyPermission
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid? CompanyId { get; set; }
	public Guid? ModuleId { get; set; }
	public Guid? SubModuleId { get; set; }


	public Module? Module { get; set; }
	public SubModule? SubModule { get; set; }
	//public Company? Company { get; set; }
}

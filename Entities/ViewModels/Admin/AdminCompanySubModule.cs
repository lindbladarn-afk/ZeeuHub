namespace Entities.ViewModels.Admin;

public class AdminCompanySubModule
{
    public Guid Id { get; set; }
    public Guid ModulId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool HasAccess { get; set; }
}

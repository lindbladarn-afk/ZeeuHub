namespace Entities.Application;

public class CompanyConnectionString
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ConnectionStringTypeId { get; set; }
    public string? ConnectionString { get; set; } = null;
    public string? DatabaseName { get; set; }
    public bool IsActive { get; set; }

}
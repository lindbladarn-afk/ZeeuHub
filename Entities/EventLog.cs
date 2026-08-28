namespace Entities;

public class EventLog
{
    public int Id { get; set; }
    public DateTime CreatedDt { get; set; }
    public Guid CompanyId { get; set; }
    public string Action { get; set; }
    public string? Message { get; set; }
    public bool ErrorOccured { get; set; }
    public string? ErrorMessage { get; set; }
}

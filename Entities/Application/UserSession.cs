using System.Collections.Generic;
using System.Text.Json.Serialization;
using Entities.User;

namespace Entities.Application;

public class UserSession
{
    public string UserId { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Language { get; set; }
    public string? CompanyName { get; set; }
    public string? PersSign { get; set; }
    public Guid? CompanyId { get; set; }
    public int? JeevesActiveCompany { get; set; }
    public bool HasDataAccess { get; set; } = true;
    public string? DataAccessWarning { get; set; }
    [JsonIgnore]
    public IEnumerable<JeevesCompanyVM>? JeevesCompanies { get; set; }
}

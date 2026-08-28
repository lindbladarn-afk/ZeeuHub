namespace WebApp.Services.Integration.CustomerSync.HubSpot;

// Describes the result of a HubSpot customer create or update operation.
public sealed class HubSpotCustomerWriteResult
{
    public string? CompanyId { get; set; }
    public string? ContactId { get; set; }
    public bool Created { get; set; }
    public bool Changed { get; set; }
}

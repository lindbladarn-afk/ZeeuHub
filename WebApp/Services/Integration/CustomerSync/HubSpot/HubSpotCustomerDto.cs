namespace WebApp.Services.Integration.CustomerSync.HubSpot;

// Carries the HubSpot customer fields CustomerSync needs before mapping to the shared domain model.
public sealed class HubSpotCustomerDto
{
    public string? CompanyId { get; set; }
    public string? ContactId { get; set; }
    public string? OrganizationNumber { get; set; }
    public string? Name { get; set; }
    public string? Domain { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

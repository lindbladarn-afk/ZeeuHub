namespace WebApp.Services.Integration.CustomerSync.Jeeves;

// Carries the Jeeves customer fields CustomerSync needs before mapping to the shared domain model.
public sealed class JeevesCustomerDto
{
    public string? CustomerNumber { get; set; }
    public string? OrganizationNumber { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

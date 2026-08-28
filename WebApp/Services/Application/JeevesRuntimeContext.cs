namespace WebApp.Services.Application;

public sealed class JeevesRuntimeContext
{
    public string UserId { get; init; } = string.Empty;
    public Guid CompanyId { get; init; }
    public int CompanyCode { get; init; }
    public string ConnectionString { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? PersSign { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
}

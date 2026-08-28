namespace WebApp.Models.Application;

public sealed class PortalAuthenticationTicketRecord
{
    public string Id { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public byte[] Payload { get; set; } = [];
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

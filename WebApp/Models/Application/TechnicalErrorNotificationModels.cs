namespace WebApp.Models.Application;

public sealed class TechnicalNotificationOptions
{
    public const string SectionName = "TechnicalNotifications";

    public bool Enabled { get; set; } = true;
    public string SubjectPrefix { get; set; } = "Portal technical error";
    public string? To { get; set; }
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
}

public sealed class TechnicalErrorNotificationRequest
{
    public string Module { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Details { get; set; }
    public Guid? CompanyId { get; set; }
    public int? JeevesCompanyCode { get; set; }
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? RequestPath { get; set; }
    public Exception? Exception { get; set; }
}

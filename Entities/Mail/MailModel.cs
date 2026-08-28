namespace Entities.Mail;

public class MailModel : IMailModel
{
    public string To { get; set; }
    public string Subject { get; set; }
    public string? Header { get; set; }
    public string? Text { get; set; }
    public string? VerificationURL { get; set; }
    public string? VerificationUrlText { get; set; }
    public string? FirstName { get; set; }
    public string? Company { get; set; }
    public string? ErrorMessage { get; set; }
}

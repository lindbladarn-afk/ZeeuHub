namespace Entities.Mail;

public interface IMailModel
{
    string? Company { get; set; }
    string? FirstName { get; set; }
    string? Header { get; set; }
    string Subject { get; set; }
    string? Text { get; set; }
    string To { get; set; }
    string? VerificationURL { get; set; }
    string? VerificationUrlText { get; set; }
    string? ErrorMessage { get; set; }
}
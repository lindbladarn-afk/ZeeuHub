using Entities.Mail;

namespace MailService
{
    public interface IMailManager
    {
        Task SendVerificationMailAsync(IMailModel model);

        Task SendNotificationMailAsync(IMailModel model);

        Task SendNotificationMailAsync(
            IMailModel model,
            string? htmlOverride,
            IReadOnlyCollection<string>? toRecipients = null,
            IReadOnlyCollection<string>? ccRecipients = null,
            IReadOnlyCollection<string>? bccRecipients = null);
    }
}

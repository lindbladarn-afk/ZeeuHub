using Azure.Identity;
using Entities.Mail;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using System.Reflection;
namespace MailService
{
    public class MailManager : IMailManager
    {
        public async Task SendVerificationMailAsync(IMailModel model)
        {
            // Define your credentials based on the created app and user details.
            // Specify the options. In most cases we're running the Azure Public Cloud.
            var credentials = new ClientSecretCredential(
                Environment.GetEnvironmentVariable("ZeeuCustomerPortal_TenantID"),
                Environment.GetEnvironmentVariable("ZeeuCustomerPortal_ClientID"),
                Environment.GetEnvironmentVariable("ZeeuCustomerPortal_ClientSecret"),
                new TokenCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzurePublicCloud });


            // Define our new Microsoft Graph client.
            // Use the credentials we specified above.
            GraphServiceClient graphServiceClient = new GraphServiceClient(credentials);

            // Define something for the message. 
            // I'm getting the HTML e-mail template and replacing a few entries, for demonstration purposes. 
            // Real-world implementations of this would use a more robust templating experience, with more options.

            var subject = $"{model.Subject}";
            var body = VerificationMailTemplateRenderer.RenderBody(model);

            // Define a simple e-mail message.
            var message = new Message
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = body
                },
                ToRecipients = new List<Recipient>()
                {
                    new Recipient { EmailAddress = new EmailAddress { Address = VerificationMailRouting.ResolveRecipient(model.To) }}
                }
            };

            var requestBody = new SendMailPostRequestBody
            {
                Message = message,
                SaveToSentItems = true
            };

            // Send mail as the given user. 
            try
            {
                await graphServiceClient
                    .Users[Environment.GetEnvironmentVariable("ZeeuCustomerPortal_UserObjectID")]
                    .SendMail
                    .PostAsync(requestBody);
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to send Email to {model.To}. {FlattenExceptionMessages(ex)}", ex);
            }

        }


        public async Task SendNotificationMailAsync(IMailModel model)
        {
            await SendNotificationMailAsync(model, htmlOverride: null, toRecipients: null, ccRecipients: null, bccRecipients: null);
        }

        public async Task SendNotificationMailAsync(
            IMailModel model,
            string? htmlOverride,
            IReadOnlyCollection<string>? toRecipients = null,
            IReadOnlyCollection<string>? ccRecipients = null,
            IReadOnlyCollection<string>? bccRecipients = null)
        {
            var workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
            var templatePath = Path.Combine(workingDirectory, "MailTemplates", "notification.html");


            // Define your credentials based on the created app and user details.
            // Specify the options. In most cases we're running the Azure Public Cloud.
            var credentials = new ClientSecretCredential(
                Environment.GetEnvironmentVariable("ZeeuCustomerPortal_TenantID"),
                Environment.GetEnvironmentVariable("ZeeuCustomerPortal_ClientID"),
                Environment.GetEnvironmentVariable("ZeeuCustomerPortal_ClientSecret"),
                new TokenCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzurePublicCloud });


            // Define our new Microsoft Graph client.
            // Use the credentials we specified above.
            GraphServiceClient graphServiceClient = new GraphServiceClient(credentials);

            // Define something for the message. 
            // I'm getting the HTML e-mail template and replacing a few entries, for demonstration purposes. 
            // Real-world implementations of this would use a more robust templating experience, with more options.

            var subject = $"{model.Subject}";
            var body = string.IsNullOrWhiteSpace(htmlOverride)
                ? System.IO.File.ReadAllText(templatePath)
                    .Replace("{{ERROR_MESSAGE}}", model.ErrorMessage)
                    .Replace("{{HEADER}}", model.Header)
                    .Replace("{{TEXT}}", model.Text)
                : htmlOverride;

            // Define a simple e-mail message.
            var message = new Message
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = body
                },
                ToRecipients = ToRecipients(toRecipients ?? new[] { model.To }) ?? new List<Recipient>(),
                CcRecipients = ToRecipients(ccRecipients) ?? new List<Recipient>(),
                BccRecipients = ToRecipients(bccRecipients) ?? new List<Recipient>()
            };

            var requestBody = new SendMailPostRequestBody
            {
                Message = message,
                SaveToSentItems = true
            };

            // Send mail as the given user. 
            try
            {
                await graphServiceClient
                    .Users[Environment.GetEnvironmentVariable("ZeeuCustomerPortal_UserObjectID")]
                    .SendMail
                    .PostAsync(requestBody);
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to send Email to {model.To}. {FlattenExceptionMessages(ex)}", ex);
            }

        }

        private static List<Recipient>? ToRecipients(IReadOnlyCollection<string>? addresses)
        {
            if (addresses == null || addresses.Count == 0)
                return null;

            return addresses
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .SelectMany(x => x.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(x => new Recipient { EmailAddress = new EmailAddress { Address = x } })
                .ToList();
        }

        private static string FlattenExceptionMessages(Exception ex)
        {
            var messages = new List<string>();
            Exception? current = ex;

            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                    messages.Add(current.Message.Trim());

                current = current.InnerException;
            }

            return string.Join(" | ", messages.Distinct(StringComparer.Ordinal));
        }
    }
}

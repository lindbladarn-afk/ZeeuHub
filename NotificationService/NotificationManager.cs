using AspNetCoreHero.ToastNotification.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;

namespace NotificationService
{
    public class NotificationManager : INotificationManager
    {
        private readonly INotyfService _notyf;

        private readonly string red = "#fea0a4";
        private readonly string yellow = "#f8ba5f";

        public NotificationManager(INotyfService notyf)
        {
            _notyf = notyf;
        }

        public Task Success(string message)
        {
            var payload = BuildHubMessage("Klart", message, "hub-success-toast");
            _notyf.Custom(payload, 6, "#0f172a", "fas fa-check");
            return Task.CompletedTask;
        }

        public Task Error(string message)
        {
            _notyf.Custom(message, 30, red, "fas fa-exclamation");
            return Task.CompletedTask;
        }

        public Task Warning(string message)
        {
            _notyf.Custom(message, 6, yellow, "fas fa-warning");
            return Task.CompletedTask;
        }

        public Task Information(string message)
        {
            _notyf.Custom(message, 6, "whitesmoke", "fas fa-information");
            return Task.CompletedTask;
        }

        public Task HubStatus(string message)
        {
            var payload = BuildHubMessage("Status", message, "hub-status-toast");
            _notyf.Custom(payload, 10, "#0f172a", "fas fa-check");
            return Task.CompletedTask;
        }

        public Task TemporaryPassword(string email, string temporaryPassword)
        {
            var message = BuildTemporaryPasswordMessage(email, temporaryPassword);
            _notyf.Custom(message, 18, "#0f172a", "fas fa-key");
            return Task.CompletedTask;
        }

        private static string BuildHubMessage(string eyebrow, string message, string cssClass)
        {
            var encodedEyebrow = WebUtility.HtmlEncode(eyebrow ?? string.Empty);
            var encodedMessage = WebUtility.HtmlEncode(message ?? string.Empty);

            var builder = new StringBuilder();
            builder.Append("<div class=\"");
            builder.Append(cssClass);
            builder.Append("\">");
            builder.Append("<div class=\"");
            builder.Append(cssClass);
            builder.Append("__eyebrow\">");
            builder.Append(encodedEyebrow);
            builder.Append("</div>");
            builder.Append("<div class=\"");
            builder.Append(cssClass);
            builder.Append("__message\">");
            builder.Append(encodedMessage);
            builder.Append("</div>");
            builder.Append("</div>");
            return builder.ToString();
        }

        private static string BuildTemporaryPasswordMessage(string email, string temporaryPassword)
        {
            var encodedEmail = WebUtility.HtmlEncode(email ?? string.Empty);
            var encodedPassword = WebUtility.HtmlEncode(temporaryPassword ?? string.Empty);

            var builder = new StringBuilder();
            builder.Append("<div class=\"hub-password-toast\" data-hub-password-toast data-hub-password-value=\"");
            builder.Append(encodedPassword);
            builder.Append("\">");
            builder.Append("<div class=\"hub-password-toast__eyebrow\">Tillfälligt lösenord skapat</div>");
            builder.Append("<div class=\"hub-password-toast__email\">");
            builder.Append(encodedEmail);
            builder.Append("</div>");
            builder.Append("<div class=\"hub-password-toast__secret\">");
            builder.Append("<span class=\"hub-password-toast__label\">Lösenord</span>");
            builder.Append("<div class=\"hub-password-toast__secret-row\">");
            builder.Append("<code data-hub-password-value>");
            builder.Append(encodedPassword);
            builder.Append("</code>");
            builder.Append("<button type=\"button\" class=\"hub-password-toast__copy\" data-hub-password-copy aria-label=\"Kopiera lösenord\" title=\"Kopiera lösenord\"><i class=\"fas fa-copy\" aria-hidden=\"true\"></i></button>");
            builder.Append("</div>");
            builder.Append("</div>");
            builder.Append("</div>");
            return builder.ToString();
        }
    }
}

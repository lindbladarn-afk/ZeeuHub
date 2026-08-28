using Entities.Mail;

namespace WebApp.Services.Admin.Users;

// Builds the verification mail payload used by admin user flows.
public static class VerificationMailFactory
{
    public static MailModel Create(string recipientEmail, string firstName, string verificationUrl)
    {
        return new MailModel
        {
            Subject = "Confirm your ZeeU portal account!",
            To = recipientEmail,
            Header = $"Welcome {firstName}",
            Text = "Please confirm your account by clicking on the link below. The link is valid for 3 days.",
            VerificationURL = verificationUrl,
            VerificationUrlText = "Verify"
        };
    }
}

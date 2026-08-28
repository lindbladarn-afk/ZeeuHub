namespace MailService;

// Resolves where verification emails should be sent in test environments.
public static class VerificationMailRouting
{
    private const string RedirectEnabledVariable = "ZeeuCustomerPortal_VerificationMailRedirectEnabled";
    private const string RedirectToVariable = "ZeeuCustomerPortal_VerificationMailRedirectTo";

    public static string ResolveRecipient(string originalRecipient)
    {
        if (string.IsNullOrWhiteSpace(originalRecipient))
        {
            return string.Empty;
        }

        var redirectEnabled = bool.TryParse(Environment.GetEnvironmentVariable(RedirectEnabledVariable), out var enabled) && enabled;
        var redirectTo = Environment.GetEnvironmentVariable(RedirectToVariable)?.Trim();

        if (redirectEnabled && !string.IsNullOrWhiteSpace(redirectTo))
        {
            return redirectTo;
        }

        return originalRecipient.Trim();
    }
}

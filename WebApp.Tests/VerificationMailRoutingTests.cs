using MailService;

namespace WebApp.Tests;

// Verifierar hur verification-mail routas i testläge.
[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class VerificationMailRoutingCollectionDefinition
{
    public const string CollectionName = "Verification mail routing";
}

[Collection(VerificationMailRoutingCollectionDefinition.CollectionName)]
public sealed class VerificationMailRoutingTests
{
    private const string RedirectEnabledVariable = "ZeeuCustomerPortal_VerificationMailRedirectEnabled";
    private const string RedirectToVariable = "ZeeuCustomerPortal_VerificationMailRedirectTo";

    [Fact]
    public void ResolveRecipient_Returns_Override_When_Redirect_Is_Enabled()
    {
        var result = WithEnvironmentVariables(true, "Alexander.ek@zeeu.se", () =>
            VerificationMailRouting.ResolveRecipient("user@customer.se"));

        Assert.Equal("Alexander.ek@zeeu.se", result);
    }

    [Fact]
    public void ResolveRecipient_Returns_Original_When_Redirect_Is_Disabled()
    {
        var result = WithEnvironmentVariables(false, "Alexander.ek@zeeu.se", () =>
            VerificationMailRouting.ResolveRecipient("user@customer.se"));

        Assert.Equal("user@customer.se", result);
    }

    [Fact]
    public void ResolveRecipient_Returns_Empty_When_Original_Is_Blank()
    {
        var result = WithEnvironmentVariables(true, "Alexander.ek@zeeu.se", () =>
            VerificationMailRouting.ResolveRecipient("   "));

        Assert.Equal(string.Empty, result);
    }

    private static T WithEnvironmentVariables<T>(bool redirectEnabled, string redirectTo, Func<T> action)
    {
        var oldEnabled = Environment.GetEnvironmentVariable(RedirectEnabledVariable);
        var oldTo = Environment.GetEnvironmentVariable(RedirectToVariable);

        try
        {
            Environment.SetEnvironmentVariable(RedirectEnabledVariable, redirectEnabled ? "true" : "false");
            Environment.SetEnvironmentVariable(RedirectToVariable, redirectTo);

            return action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(RedirectEnabledVariable, oldEnabled);
            Environment.SetEnvironmentVariable(RedirectToVariable, oldTo);
        }
    }
}

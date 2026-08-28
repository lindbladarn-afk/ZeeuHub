using WebApp.Services.Admin.Users;

namespace WebApp.Tests;

// Verifierar innehållet i verifieringsmailet som adminflöden använder.
public sealed class VerificationMailFactoryTests
{
    [Fact]
    public void Create_Builds_Expected_VerificationMail()
    {
        var mail = VerificationMailFactory.Create(
            "user@example.com",
            "Alex",
            "https://example.com/verify");

        Assert.Equal("Confirm your ZeeU portal account!", mail.Subject);
        Assert.Equal("user@example.com", mail.To);
        Assert.Equal("Welcome Alex", mail.Header);
        Assert.Equal("Please confirm your account by clicking on the link below. The link is valid for 3 days.", mail.Text);
        Assert.Equal("https://example.com/verify", mail.VerificationURL);
        Assert.Equal("Verify", mail.VerificationUrlText);
    }
}

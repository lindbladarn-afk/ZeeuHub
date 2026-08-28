using Entities.Mail;
using MailService;

namespace WebApp.Tests;

// Verifierar att verification-mallen renderas med rätt värden.
public sealed class VerificationMailTemplateRendererTests
{
    [Fact]
    public void RenderBody_Replaces_All_Placeholders()
    {
        var template = "<html><body><h1>{{HEADER}}</h1><p>{{TEXT}}</p><a href=\"{{VERIFICATION_LINK}}\">{{VERIFICATION_LINK_TEXT}}</a></body></html>";
        var model = new MailModel
        {
            Header = "Welcome Alex",
            Text = "Click to verify",
            VerificationURL = "https://example.com/verify",
            VerificationUrlText = "Verify"
        };

        var result = VerificationMailTemplateRenderer.RenderBody(template, model);

        Assert.Contains("Welcome Alex", result);
        Assert.Contains("Click to verify", result);
        Assert.Contains("https://example.com/verify", result);
        Assert.Contains("Verify", result);
    }
}

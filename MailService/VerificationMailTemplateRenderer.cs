using Entities.Mail;
using System.Reflection;

namespace MailService;

// Renders the verification mail template used by both sending and preview flows.
public static class VerificationMailTemplateRenderer
{
    private const string TemplateFolderName = "MailTemplates";
    private const string TemplateFileName = "verification.html";

    public static string RenderBody(IMailModel model)
    {
        var templatePath = GetTemplatePath();
        var templateHtml = File.ReadAllText(templatePath);
        return RenderBody(templateHtml, model);
    }

    public static string RenderBody(string templateHtml, IMailModel model)
    {
        return templateHtml
            .Replace("{{VERIFICATION_LINK}}", model.VerificationURL ?? string.Empty)
            .Replace("{{VERIFICATION_LINK_TEXT}}", model.VerificationUrlText ?? string.Empty)
            .Replace("{{HEADER}}", model.Header ?? string.Empty)
            .Replace("{{TEXT}}", model.Text ?? string.Empty);
    }

    private static string GetTemplatePath()
    {
        var workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? AppContext.BaseDirectory;
        return Path.Combine(workingDirectory, TemplateFolderName, TemplateFileName);
    }
}

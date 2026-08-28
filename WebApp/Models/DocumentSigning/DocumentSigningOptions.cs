using System.ComponentModel.DataAnnotations;

namespace WebApp.Models.DocumentSigning;

public class DocumentSigningOptions
{
    public const string SectionName = "DocumentSigning";
    public Dictionary<string, DocumentSigningProviderOptions> Companies { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class DocumentSigningProviderOptions
{
    public Guid? CompanyId { get; set; }
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://api.oneflow.com/";
    public string ApiToken { get; set; } = string.Empty;
    public string OneflowUserEmail { get; set; } = string.Empty;
    public int? OneflowWorkspaceId { get; set; }
    public int? OneflowTemplateId { get; set; }
    public string OneflowCounterpartyCountryCode { get; set; } = "SE";
    public string DefaultInvitationMessage { get; set; } = string.Empty;

    public bool IsConfigured()
    {
        return Enabled
            && !string.IsNullOrWhiteSpace(ApiToken)
            && !string.IsNullOrWhiteSpace(OneflowUserEmail)
            && OneflowWorkspaceId.HasValue
            && OneflowTemplateId.HasValue;
    }

    public bool CanPing()
    {
        return Enabled
            && !string.IsNullOrWhiteSpace(ApiToken);
    }
}

public sealed class DocumentSigningUploadFile
{
    public DocumentSigningUploadFile(string fileName, byte[] content)
    {
        FileName = fileName;
        Content = content;
    }

    [Required]
    public string FileName { get; }

    [Required]
    public byte[] Content { get; }
}

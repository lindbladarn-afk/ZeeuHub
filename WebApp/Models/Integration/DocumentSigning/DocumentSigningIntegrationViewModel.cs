using WebApp.Models.Orders;
using WebApp.Models.DocumentSigning;

namespace WebApp.Models.Integration;

public sealed class DocumentSigningIntegrationViewModel
{
    public bool IsConfigured { get; set; }
    public bool IsRuntimeAvailable { get; set; } = true;
    public string? RuntimeUnavailableReason { get; set; }
    public string? OneflowLookupError { get; set; }
    public int? LookupWorkspaceId { get; set; }
    public IReadOnlyList<DocumentSigningOneflowWorkspaceViewModel> OneflowWorkspaces { get; set; } = Array.Empty<DocumentSigningOneflowWorkspaceViewModel>();
    public IReadOnlyList<DocumentSigningOneflowTemplateViewModel> OneflowTemplates { get; set; } = Array.Empty<DocumentSigningOneflowTemplateViewModel>();
    public long? SelectedOrderNo { get; set; }
    public string? SelectedOrderCustomerName { get; set; }
    public bool OrderExists { get; set; }
    public Guid? SelectedSigningId { get; set; }
    public DocumentSigningListItem? SelectedSigning { get; set; }
    public IReadOnlyList<DocumentSigningListItem> Signings { get; set; } = Array.Empty<DocumentSigningListItem>();
    public OrderDocumentSigningFormViewModel DocumentSigningForm { get; set; } = new();
}

public sealed class DocumentSigningOneflowWorkspaceViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class DocumentSigningOneflowTemplateViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<int> WorkspaceIds { get; set; } = Array.Empty<int>();
}

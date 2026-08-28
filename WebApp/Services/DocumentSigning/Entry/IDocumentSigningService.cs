using WebApp.Models.DocumentSigning;
using WebApp.Models.Integration;
using WebApp.ViewModels.DocumentSigning;

namespace WebApp.Services.DocumentSigning;

// Defines the application-facing operations for document signing flows.
public interface IDocumentSigningService
{
    bool IsEnabledForCompany(Guid companyId);
    bool CanPingForCompany(Guid companyId);
    Task<IReadOnlyList<DocumentSigningListItem>> ListForOrderAsync(Guid companyId, int? jeevesCompanyCode, long orderNo, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentSigningListItem>> ListRecentAsync(Guid companyId, int? jeevesCompanyCode, int take = 20, CancellationToken cancellationToken = default);
    Task<DocumentSigningCreateResult> CreateAndStartAsync(DocumentSigningCreateRequest request, CancellationToken cancellationToken = default);
    Task<DocumentSigningListItem?> SyncAsync(Guid companyId, Guid signingId, CancellationToken cancellationToken = default);
    Task<DocumentSigningLaunchResult?> LaunchAsync(Guid companyId, Guid signingId, CancellationToken cancellationToken = default);
    Task PingAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentSigningOneflowWorkspaceViewModel>> ListWorkspacesAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentSigningOneflowTemplateViewModel>> ListTemplatesAsync(Guid companyId, int? workspaceId = null, CancellationToken cancellationToken = default);
    Task<DocumentSigningPublicResultViewModel?> GetPublicResultAsync(Guid publicToken, CancellationToken cancellationToken = default);
}

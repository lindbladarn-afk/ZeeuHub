using WebApp.ViewModels.Admin.ApprovalChains;

namespace WebApp.Services.Admin.ApprovalChains;

// Builds the approval-chain designer model from the portal-owned rule source.
public interface IApprovalChainDesignerService
{
    Task<ApprovalChainDesignerViewModel> BuildAsync(short companyCode, CancellationToken cancellationToken = default);
}

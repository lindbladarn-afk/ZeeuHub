using WebApp.ViewModels.Admin.ApprovalChains;

namespace WebApp.Services.Admin.ApprovalChains;

// Compatibility wrapper for the approval-chain designer page.
// The actual seed data lives in ApprovalChainCatalog so the UI and future persistence can share it.
public static class ApprovalChainDesignerDataBuilder
{
    public static ApprovalChainDesignerViewModel Build()
        => ApprovalChainCatalog.BuildDesignerModel();
}

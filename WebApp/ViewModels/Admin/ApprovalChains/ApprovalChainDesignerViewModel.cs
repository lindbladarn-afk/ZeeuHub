namespace WebApp.ViewModels.Admin.ApprovalChains;

// View model for the approval-chain designer and simulation page.
// Keeps the UI data-shaped so the page can be rendered now and wired to a real source later.
public sealed class ApprovalChainDesignerViewModel
{
    public string PageTitle { get; set; } = "Attestkedja";
    public string PageSubtitle { get; set; } = "Simulera, jämför och förhandsgranska attestkedjor per ordertyp.";
    public string SelectedOrderTypeKey { get; set; } = "purchase";
    public decimal SelectedAmount { get; set; } = 150000m;
    public string Currency { get; set; } = "SEK";

    public IReadOnlyList<ApprovalChainOrderTypeViewModel> OrderTypes { get; set; } = Array.Empty<ApprovalChainOrderTypeViewModel>();
    public IReadOnlyList<ApprovalChainRolePresetViewModel> RolePresets { get; set; } = Array.Empty<ApprovalChainRolePresetViewModel>();
    public IReadOnlyList<ApprovalChainApproverOptionViewModel> ApproverOptions { get; set; } = Array.Empty<ApprovalChainApproverOptionViewModel>();
}

public sealed class ApprovalChainOrderTypeViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
    public string AccentClass { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<decimal> QuickAmounts { get; set; } = Array.Empty<decimal>();
    public IReadOnlyList<ApprovalChainStepViewModel> Steps { get; set; } = Array.Empty<ApprovalChainStepViewModel>();
}

public sealed class ApprovalChainStepViewModel
{
    public int Sequence { get; set; }
    public string RoleKey { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string ApproverKey { get; set; } = string.Empty;
    public string ApproverName { get; set; } = string.Empty;
    public string ReserveApproverName { get; set; } = string.Empty;
    public decimal? Limit { get; set; }
    public decimal? NegativeLimit { get; set; }
    public string Note { get; set; } = string.Empty;
}

public sealed class ApprovalChainRolePresetViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string AccentClass { get; set; } = string.Empty;
}

public sealed class ApprovalChainApproverOptionViewModel
{
    public string Key { get; set; } = string.Empty;
    public string RoleKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AccentClass { get; set; } = string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Title : $"{Title} — {Name}";
}

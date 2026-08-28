using WebApp.ViewModels.Admin.ApprovalChains;

namespace WebApp.Services.Admin.ApprovalChains;

// Canonical portal approval-chain definitions used by the simulator and future persistence.
public sealed record ApprovalChainRuleSet(
    string Key,
    string Title,
    string IconClass,
    string AccentClass,
    string Description,
    IReadOnlyList<decimal> QuickAmounts,
    IReadOnlyList<ApprovalChainRuleStep> Steps);

// One approval step in a flow. The portal keeps the step data explicit so we can mirror it into SQL later.
public sealed record ApprovalChainRuleStep(
    int Sequence,
    string RoleKey,
    string RoleName,
    string ApproverKey,
    string ApproverName,
    string ReserveApproverName,
    decimal? Limit,
    decimal? NegativeLimit,
    bool IsDefault,
    bool SendMail,
    string Note);

// Static catalogue for the current portal demo data.
public static class ApprovalChainCatalog
{
    public static ApprovalChainDesignerViewModel BuildDesignerModel()
    {
        return new ApprovalChainDesignerViewModel
        {
            PageTitle = "Attestkedja",
            PageSubtitle = "Simulera belopp, jämför ordertyper och se vem som attesterar innan flödet går vidare.",
            SelectedOrderTypeKey = "purchase",
            SelectedAmount = 150000m,
            Currency = "SEK",
            RolePresets = GetRolePresets(),
            ApproverOptions = GetApproverOptions(),
            OrderTypes = GetOrderTypes().Select(MapOrderType).ToList()
        };
    }

    public static IReadOnlyList<ApprovalChainRuleSet> GetOrderTypes()
    {
        return new[]
        {
            new ApprovalChainRuleSet(
                Key: "purchase",
                Title: "Inköp / Material",
                IconClass: "fa fa-shopping-cart",
                AccentClass: "approval-type--teal",
                Description: "Standardflöde för inköp där beloppet avgör hur långt ärendet går i kedjan.",
                QuickAmounts: new[] { 5000m, 25000m, 75000m, 150000m },
                Steps: new[]
                {
                    new ApprovalChainRuleStep(1, "chef", "Chef", "anna-lindstrom", "Anna Lindström", "Marcus Ek", 25000m, -25000m, false, true, "Startnivå för vardagsinköp."),
                    new ApprovalChainRuleStep(2, "controller", "Controller", "marcus-ek", "Marcus Ek", "Helena Björk", 100000m, -100000m, false, true, "Krävs när beloppet växer."),
                    new ApprovalChainRuleStep(3, "cfo", "CFO", "helena-bjork", "Helena Björk", "Anna Lindström", null, null, true, true, "Slutled för höga belopp.")
                }
            ),
            new ApprovalChainRuleSet(
                Key: "service",
                Title: "Tjänst / Konsult",
                IconClass: "fa fa-handshake",
                AccentClass: "approval-type--indigo",
                Description: "För inköp av tjänster där attestbehov ofta styrs av avtal, timmar och projekt.",
                QuickAmounts: new[] { 5000m, 20000m, 50000m, 100000m },
                Steps: new[]
                {
                    new ApprovalChainRuleStep(1, "chef", "Chef", "sara-holm", "Sara Holm", "Jonas Berg", 15000m, -15000m, false, true, "För mindre konsultköp."),
                    new ApprovalChainRuleStep(2, "controller", "Controller", "jonas-berg", "Jonas Berg", "Helena Björk", 50000m, -50000m, false, true, "Granskning av budget och projekt."),
                    new ApprovalChainRuleStep(3, "cfo", "CFO", "helena-bjork", "Helena Björk", "Sara Holm", null, null, true, true, "Högsta nivå för större upplägg.")
                }
            ),
            new ApprovalChainRuleSet(
                Key: "travel",
                Title: "Resa / Hotell",
                IconClass: "fa fa-plane",
                AccentClass: "approval-type--cyan",
                Description: "För resebokningar och hotell där policy, säsong och destination påverkar flödet.",
                QuickAmounts: new[] { 3000m, 10000m, 25000m, 50000m },
                Steps: new[]
                {
                    new ApprovalChainRuleStep(1, "chef", "Chef", "karin-stahl", "Karin Ståhl", "Peter Nordin", 10000m, -10000m, false, true, "Normal resepolicy."),
                    new ApprovalChainRuleStep(2, "controller", "Controller", "peter-nordin", "Peter Nordin", "Helena Björk", 30000m, -30000m, false, true, "Om resan går över normalramen."),
                    new ApprovalChainRuleStep(3, "cfo", "CFO", "helena-bjork", "Helena Björk", "Karin Ståhl", null, null, true, true, "Undantag och större konferenser.")
                }
            ),
            new ApprovalChainRuleSet(
                Key: "software",
                Title: "Programvara / Licens",
                IconClass: "fa fa-laptop",
                AccentClass: "approval-type--amber",
                Description: "För licenser och mjukvara där återkommande kostnad och affärsnytta ska vägas in.",
                QuickAmounts: new[] { 5000m, 15000m, 50000m, 150000m },
                Steps: new[]
                {
                    new ApprovalChainRuleStep(1, "chef", "Chef", "maja-fors", "Maja Fors", "Marcus Ek", 10000m, -10000m, false, true, "Mindre licenser och verktyg."),
                    new ApprovalChainRuleStep(2, "controller", "Controller", "marcus-ek", "Marcus Ek", "Helena Björk", 40000m, -40000m, false, true, "Fleråriga avtal eller plattformslicenser."),
                    new ApprovalChainRuleStep(3, "cfo", "CFO", "helena-bjork", "Helena Björk", "Maja Fors", null, null, true, true, "Större åtaganden eller strategiska val.")
                }
            ),
            new ApprovalChainRuleSet(
                Key: "investment",
                Title: "Investering",
                IconClass: "fa fa-chart-line",
                AccentClass: "approval-type--green",
                Description: "För investeringar med högre risk, längre horisont och tydligare beslutskrav.",
                QuickAmounts: new[] { 25000m, 75000m, 150000m, 300000m },
                Steps: new[]
                {
                    new ApprovalChainRuleStep(1, "chef", "Chef", "anna-lindstrom", "Anna Lindström", "Sara Holm", 25000m, -25000m, false, true, "Förankring i linjen."),
                    new ApprovalChainRuleStep(2, "controller", "Controller", "marcus-ek", "Marcus Ek", "Jonas Berg", 100000m, -100000m, false, true, "Koppling till budget och plan."),
                    new ApprovalChainRuleStep(3, "cfo", "CFO", "helena-bjork", "Helena Björk", "Anna Lindström", null, null, true, true, "Sista godkännandet.")
                }
            )
        };
    }

    public static IReadOnlyList<ApprovalChainRolePresetViewModel> GetRolePresets()
    {
        return new[]
        {
            new ApprovalChainRolePresetViewModel { Key = "chef", Title = "Chef", Subtitle = "Första linjen", AccentClass = "approval-role--teal" },
            new ApprovalChainRolePresetViewModel { Key = "controller", Title = "Controller", Subtitle = "Ekonomi", AccentClass = "approval-role--indigo" },
            new ApprovalChainRolePresetViewModel { Key = "cfo", Title = "CFO", Subtitle = "Slutnivå", AccentClass = "approval-role--amber" },
            new ApprovalChainRolePresetViewModel { Key = "backup", Title = "Reserv", Subtitle = "Fallback", AccentClass = "approval-role--rose" }
        };
    }

    public static IReadOnlyList<ApprovalChainApproverOptionViewModel> GetApproverOptions()
    {
        return new[]
        {
            new ApprovalChainApproverOptionViewModel { Key = "anna-lindstrom", RoleKey = "chef", Title = "Chef", Name = "Anna Lindström", AccentClass = "approval-role--teal" },
            new ApprovalChainApproverOptionViewModel { Key = "sara-holm", RoleKey = "chef", Title = "Chef", Name = "Sara Holm", AccentClass = "approval-role--teal" },
            new ApprovalChainApproverOptionViewModel { Key = "karin-stahl", RoleKey = "chef", Title = "Chef", Name = "Karin Ståhl", AccentClass = "approval-role--teal" },
            new ApprovalChainApproverOptionViewModel { Key = "marcus-ek", RoleKey = "controller", Title = "Controller", Name = "Marcus Ek", AccentClass = "approval-role--indigo" },
            new ApprovalChainApproverOptionViewModel { Key = "jonas-berg", RoleKey = "controller", Title = "Controller", Name = "Jonas Berg", AccentClass = "approval-role--indigo" },
            new ApprovalChainApproverOptionViewModel { Key = "helena-bjork", RoleKey = "cfo", Title = "CFO", Name = "Helena Björk", AccentClass = "approval-role--amber" },
            new ApprovalChainApproverOptionViewModel { Key = "peter-nordin", RoleKey = "backup", Title = "Reserv", Name = "Peter Nordin", AccentClass = "approval-role--rose" }
        };
    }

    public static ApprovalChainOrderTypeViewModel MapOrderType(ApprovalChainRuleSet ruleSet)
    {
        return new ApprovalChainOrderTypeViewModel
        {
            Key = ruleSet.Key,
            Title = ruleSet.Title,
            IconClass = ruleSet.IconClass,
            AccentClass = ruleSet.AccentClass,
            Description = ruleSet.Description,
            QuickAmounts = ruleSet.QuickAmounts,
            Steps = ruleSet.Steps.Select(MapStep).ToList()
        };
    }

    public static ApprovalChainStepViewModel MapStep(ApprovalChainRuleStep step)
    {
        return new ApprovalChainStepViewModel
        {
            Sequence = step.Sequence,
            RoleKey = step.RoleKey,
            RoleName = step.RoleName,
            ApproverKey = step.ApproverKey,
            ApproverName = step.ApproverName,
            ReserveApproverName = step.ReserveApproverName,
            Limit = step.Limit,
            NegativeLimit = step.NegativeLimit,
            Note = step.Note
        };
    }
}

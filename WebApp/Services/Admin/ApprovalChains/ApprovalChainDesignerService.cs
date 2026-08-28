using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Admin.ApprovalChains;
using WebApp.ViewModels.Admin.ApprovalChains;

namespace WebApp.Services.Admin.ApprovalChains;

// Reads approval-chain rules from the portal database and shapes them for the existing designer UI.
public sealed class ApprovalChainDesignerService : IApprovalChainDesignerService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ApprovalChainDesignerService> _logger;

    public ApprovalChainDesignerService(ApplicationDbContext context, ILogger<ApprovalChainDesignerService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApprovalChainDesignerViewModel> BuildAsync(short companyCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var rules = await _context.ApprovalChainRules!
                .AsNoTracking()
                .Where(rule => rule.ForetagKod == companyCode)
                .OrderBy(rule => rule.FlowId)
                .ThenBy(rule => rule.PurchaseOrderType)
                .ThenBy(rule => rule.SalesOrderType)
                .ThenBy(rule => rule.PriceListId)
                .ThenBy(rule => rule.Limit)
                .ThenBy(rule => rule.SqlIdentity)
                .ToListAsync(cancellationToken);

            if (rules.Count == 0)
                return ApprovalChainDesignerDataBuilder.Build();

            return BuildFromRules(companyCode, rules);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Approval chains could not be loaded from portal rules for company {CompanyCode}. Falling back to demo data.", companyCode);
            return ApprovalChainDesignerDataBuilder.Build();
        }
    }

    private static ApprovalChainDesignerViewModel BuildFromRules(short companyCode, IReadOnlyList<ApprovalChainRuleRecord> rules)
    {
        var orderTypes = rules
            .GroupBy(rule => new ApprovalChainRuleGroupKey(
                rule.FlowId,
                rule.PurchaseOrderType,
                rule.SalesOrderType,
                rule.PriceListId))
            .Select(group => MapOrderType(group.Key, group.ToList()))
            .OrderBy(type => type.Key)
            .ToList();

        var approverOptions = rules
            .Select(rule => rule.NextApproverPersSign)
            .Concat(rules.Select(rule => rule.CurrentApproverPersSign))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value)
            .Select(value => new ApprovalChainApproverOptionViewModel
            {
                Key = value,
                RoleKey = "approver",
                Title = "Attestant",
                Name = value,
                AccentClass = "approval-role--teal"
            })
            .ToList();

        return new ApprovalChainDesignerViewModel
        {
            PageTitle = "Attestkedja",
            PageSubtitle = $"Regler laddade från portalens attestkedja för bolag {companyCode}.",
            SelectedOrderTypeKey = orderTypes.FirstOrDefault()?.Key ?? "purchase",
            SelectedAmount = ResolveSelectedAmount(orderTypes),
            Currency = "SEK",
            RolePresets = new[]
            {
                new ApprovalChainRolePresetViewModel { Key = "approver", Title = "Attestant", Subtitle = "Jeeves PersSign", AccentClass = "approval-role--teal" },
                new ApprovalChainRolePresetViewModel { Key = "default", Title = "Standard", Subtitle = "Fallback", AccentClass = "approval-role--indigo" }
            },
            ApproverOptions = approverOptions,
            OrderTypes = orderTypes
        };
    }

    private static ApprovalChainOrderTypeViewModel MapOrderType(ApprovalChainRuleGroupKey key, IReadOnlyList<ApprovalChainRuleRecord> rules)
    {
        var sortedRules = rules
            .OrderBy(rule => rule.Limit)
            .ThenBy(rule => rule.SqlIdentity)
            .ToList();

        return new ApprovalChainOrderTypeViewModel
        {
            Key = BuildOrderTypeKey(key),
            Title = BuildOrderTypeTitle(key),
            IconClass = ResolveIconClass(key.FlowId),
            AccentClass = ResolveAccentClass(key.FlowId),
            Description = BuildOrderTypeDescription(key),
            QuickAmounts = BuildQuickAmounts(sortedRules),
            Steps = sortedRules
                .Select((rule, index) => MapStep(rule, index + 1))
                .ToList()
        };
    }

    private static ApprovalChainStepViewModel MapStep(ApprovalChainRuleRecord rule, int sequence)
    {
        var nextApprover = string.IsNullOrWhiteSpace(rule.NextApproverPersSign)
            ? "-"
            : rule.NextApproverPersSign;

        return new ApprovalChainStepViewModel
        {
            Sequence = sequence,
            RoleKey = rule.IsDefault ? "default" : "approver",
            RoleName = rule.IsDefault ? "Standard" : "Attestant",
            ApproverKey = nextApprover,
            ApproverName = nextApprover,
            ReserveApproverName = string.IsNullOrWhiteSpace(rule.CurrentApproverPersSign) ? "Start" : rule.CurrentApproverPersSign,
            Limit = rule.Limit == 0 ? null : rule.Limit,
            NegativeLimit = rule.NegativeLimit == 0 ? null : rule.NegativeLimit,
            Note = BuildStepNote(rule)
        };
    }

    private static IReadOnlyList<decimal> BuildQuickAmounts(IReadOnlyList<ApprovalChainRuleRecord> rules)
    {
        var limits = rules
            .Select(rule => rule.Limit)
            .Where(limit => limit > 0 && limit < 9_000_000m)
            .Distinct()
            .OrderBy(limit => limit)
            .Take(4)
            .ToList();

        return limits.Count > 0
            ? limits
            : new[] { 5_000m, 25_000m, 75_000m, 150_000m };
    }

    private static decimal ResolveSelectedAmount(IReadOnlyList<ApprovalChainOrderTypeViewModel> orderTypes)
    {
        return orderTypes.FirstOrDefault()?.QuickAmounts.LastOrDefault() ?? 150_000m;
    }

    private static string BuildOrderTypeKey(ApprovalChainRuleGroupKey key)
    {
        return key.FlowId switch
        {
            0 => key.PurchaseOrderType.HasValue ? $"purchase-{key.PurchaseOrderType.Value}" : "purchase",
            1 => key.SalesOrderType.HasValue ? $"sales-{key.SalesOrderType.Value}" : "sales",
            2 => key.PriceListId.HasValue ? $"price-list-{key.PriceListId.Value}" : "price-list",
            _ => $"flow-{key.FlowId}"
        };
    }

    private static string BuildOrderTypeTitle(ApprovalChainRuleGroupKey key)
    {
        return key.FlowId switch
        {
            0 => key.PurchaseOrderType.HasValue ? $"Inköp / Material ({key.PurchaseOrderType.Value})" : "Inköp / Material",
            1 => key.SalesOrderType.HasValue ? $"Kundorder ({key.SalesOrderType.Value})" : "Kundorder",
            2 => key.PriceListId.HasValue ? $"Prislista ({key.PriceListId.Value})" : "Prislista",
            _ => $"Flöde {key.FlowId}"
        };
    }

    private static string BuildOrderTypeDescription(ApprovalChainRuleGroupKey key)
    {
        return key.FlowId switch
        {
            0 => "Regler för inköpsorder där BestTyp och belopp styr nästa attestant.",
            1 => "Regler för kundorder där OrdTyp och ordervärde styr nästa attestant.",
            2 => "Regler för prislista där prislistan och beloppet styr nästa attestant.",
            _ => "Regler importerade från portalens attestkedja."
        };
    }

    private static string BuildStepNote(ApprovalChainRuleRecord rule)
    {
        var ruleText = rule.IsDefault
            ? "Startregel för ärenden utan tidigare attestant"
            : "Nästa steg i attestkedjan";
        var mailText = rule.SendMail ? "skickar mail" : "skickar inte mail";
        return $"{ruleText}, {mailText}.";
    }

    private static string ResolveIconClass(int flowId)
    {
        return flowId switch
        {
            0 => "fa fa-shopping-cart",
            1 => "fa fa-list-alt",
            2 => "fa fa-tags",
            _ => "fa fa-check-square-o"
        };
    }

    private static string ResolveAccentClass(int flowId)
    {
        return flowId switch
        {
            0 => "approval-type--teal",
            1 => "approval-type--indigo",
            2 => "approval-type--amber",
            _ => "approval-type--green"
        };
    }

    private sealed record ApprovalChainRuleGroupKey(
        int FlowId,
        short? PurchaseOrderType,
        short? SalesOrderType,
        int? PriceListId);
}

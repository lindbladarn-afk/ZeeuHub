namespace WebApp.Services.Admin.ApprovalChains;

// Compares the portal-owned purchase approval rule engine against Jeeves without writing to either system.
public sealed class ApprovalChainPurchaseParityService : IApprovalChainPurchaseParityService
{
    private readonly IApprovalChainPurchaseDecisionService _portalDecisionService;
    private readonly IApprovalChainPurchaseJeevesReader _jeevesReader;

    public ApprovalChainPurchaseParityService(
        IApprovalChainPurchaseDecisionService portalDecisionService,
        IApprovalChainPurchaseJeevesReader jeevesReader)
    {
        _portalDecisionService = portalDecisionService;
        _jeevesReader = jeevesReader;
    }

    public async Task<ApprovalChainPurchaseParityResult> CompareAsync(
        ApprovalChainPurchaseParityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = await _jeevesReader.GetOrderAsync(
            request.JeevesConnectionString,
            request.CompanyCode,
            request.PurchaseOrderNumber,
            cancellationToken);

        if (order is null)
        {
            var missingOrderDecision = CreateNoRuleDecision("Purchase order was not found in Jeeves.");
            return new ApprovalChainPurchaseParityResult(
                null,
                missingOrderDecision,
                missingOrderDecision,
                false,
                new[] { "Purchase order was not found in Jeeves." });
        }

        var portalDecision = await _portalDecisionService.EvaluateAsync(
            new ApprovalChainPurchaseDecisionRequest(
                request.CompanyCode,
                request.FlowId,
                request.CurrentApproverPersSign,
                order.PurchaseOrderType,
                order.OrderValue),
            cancellationToken);

        var legacyDecision = await _jeevesReader.EvaluateLegacyDecisionAsync(
            request.JeevesConnectionString,
            request,
            order,
            cancellationToken);

        var differences = BuildDifferences(portalDecision, legacyDecision);
        return new ApprovalChainPurchaseParityResult(
            order,
            portalDecision,
            legacyDecision,
            differences.Count == 0,
            differences);
    }

    private static List<string> BuildDifferences(
        ApprovalChainPurchaseDecision portalDecision,
        ApprovalChainPurchaseDecision legacyDecision)
    {
        var differences = new List<string>();

        AddDifference(differences, nameof(portalDecision.Kind), portalDecision.Kind, legacyDecision.Kind);
        AddDifference(differences, nameof(portalDecision.NextApproverPersSign), portalDecision.NextApproverPersSign, legacyDecision.NextApproverPersSign);
        AddDifference(differences, nameof(portalDecision.Limit), portalDecision.Limit, legacyDecision.Limit);
        AddDifference(differences, nameof(portalDecision.NegativeLimit), portalDecision.NegativeLimit, legacyDecision.NegativeLimit);
        AddDifference(differences, nameof(portalDecision.UsedDefaultRule), portalDecision.UsedDefaultRule, legacyDecision.UsedDefaultRule);
        AddDifference(differences, nameof(portalDecision.SendMail), portalDecision.SendMail, legacyDecision.SendMail);

        return differences;
    }

    private static void AddDifference<T>(List<string> differences, string fieldName, T portalValue, T legacyValue)
    {
        if (EqualityComparer<T>.Default.Equals(portalValue, legacyValue))
            return;

        differences.Add($"{fieldName}: portal={FormatValue(portalValue)}, jeeves={FormatValue(legacyValue)}");
    }

    private static string FormatValue<T>(T value)
        => value?.ToString() ?? "<null>";

    private static ApprovalChainPurchaseDecision CreateNoRuleDecision(string message)
        => new(
            ApprovalChainDecisionKind.NoRule,
            null,
            null,
            null,
            null,
            false,
            false,
            message);
}

public interface IApprovalChainPurchaseParityService
{
    Task<ApprovalChainPurchaseParityResult> CompareAsync(
        ApprovalChainPurchaseParityRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ApprovalChainPurchaseParityRequest(
    string JeevesConnectionString,
    short CompanyCode,
    long PurchaseOrderNumber,
    int FlowId,
    string CurrentApproverPersSign);

public sealed record ApprovalChainPurchaseParityResult(
    ApprovalChainPurchaseOrderSnapshot? Order,
    ApprovalChainPurchaseDecision PortalDecision,
    ApprovalChainPurchaseDecision JeevesDecision,
    bool Matches,
    IReadOnlyList<string> Differences);

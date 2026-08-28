using Repository.Execution;

namespace WebApp.Services.Admin.ApprovalChains;

// Reads the current Jeeves purchase approval state without changing order status or sending mail.
public sealed class ApprovalChainPurchaseJeevesReader : IApprovalChainPurchaseJeevesReader
{
    private readonly IJeevesSqlExecutor _jeevesSqlExecutor;

    public ApprovalChainPurchaseJeevesReader(IJeevesSqlExecutor jeevesSqlExecutor)
    {
        _jeevesSqlExecutor = jeevesSqlExecutor;
    }

    public Task<ApprovalChainPurchaseOrderSnapshot?> GetOrderAsync(
        string connectionString,
        short companyCode,
        long purchaseOrderNumber,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP (1)
                bh.ForetagKod AS CompanyCode,
                bh.BestNr AS PurchaseOrderNumber,
                bh.BestTyp AS PurchaseOrderType,
                bh.BestValue AS OrderValue
            FROM bh WITH (READUNCOMMITTED)
            WHERE bh.ForetagKod = @CompanyCode
                AND bh.BestNr = @PurchaseOrderNumber;
            """;

        return _jeevesSqlExecutor.QueryFirstOrDefaultAsync<ApprovalChainPurchaseOrderSnapshot>(
            connectionString,
            sql,
            new { CompanyCode = companyCode, PurchaseOrderNumber = purchaseOrderNumber },
            operationName: "ApprovalChainPurchaseJeevesReader.GetOrder",
            cancellationToken: cancellationToken);
    }

    public async Task<ApprovalChainPurchaseDecision> EvaluateLegacyDecisionAsync(
        string connectionString,
        ApprovalChainPurchaseParityRequest request,
        ApprovalChainPurchaseOrderSnapshot order,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(order);

        var rule = await ReadLegacyRuleAsync(connectionString, request, cancellationToken);
        if (rule is null)
        {
            return new ApprovalChainPurchaseDecision(
                ApprovalChainDecisionKind.NoRule,
                null,
                null,
                null,
                null,
                false,
                false,
                "No approval chain rule matched the purchase order in Jeeves.");
        }

        var shouldForward = ShouldForward(Math.Abs(order.OrderValue), request.CurrentApproverPersSign, rule);
        return new ApprovalChainPurchaseDecision(
            shouldForward ? ApprovalChainDecisionKind.ForwardToNextApprover : ApprovalChainDecisionKind.FinalApprove,
            rule.NextApproverPersSign,
            rule.Limit,
            rule.NegativeLimit,
            rule.RuleSqlIdentity,
            rule.UsedDefaultRule,
            rule.SendMail,
            shouldForward
                ? "Jeeves would forward the order to the next approver."
                : "Jeeves would final-approve the order for the current approver.");
    }

    private Task<LegacyApprovalChainRule?> ReadLegacyRuleAsync(
        string connectionString,
        ApprovalChainPurchaseParityRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH OrderRow AS
            (
                SELECT TOP (1)
                    bh.ForetagKod,
                    bh.BestTyp
                FROM bh WITH (READUNCOMMITTED)
                WHERE bh.ForetagKod = @CompanyCode
                    AND bh.BestNr = @PurchaseOrderNumber
            )
            SELECT TOP (1)
                q.SQLIDENTITY AS RuleSqlIdentity,
                q.attestsign AS NextApproverPersSign,
                q.attestlimit AS Limit,
                q.q_zu_attestlimit_2 AS NegativeLimit,
                CAST(CASE WHEN q.perssign2 = @CurrentApproverPersSign THEN 0 ELSE 1 END AS bit) AS UsedDefaultRule,
                CAST(CASE WHEN q.q_zu_approval_mail = N'1' THEN 1 ELSE 0 END AS bit) AS SendMail
            FROM q_zu_approval_chains q WITH (READUNCOMMITTED)
            INNER JOIN OrderRow bh
                ON bh.ForetagKod = q.ForetagKod
                AND bh.BestTyp = q.besttyp
            INNER JOIN pr WITH (READUNCOMMITTED)
                ON pr.ForetagKod = q.ForetagKod
                AND pr.PersSign = q.attestsign
            WHERE q.ForetagKod = @CompanyCode
                AND q.q_zu_approval_flowid = @FlowId
                AND (
                    q.perssign2 = @CurrentApproverPersSign
                    OR q.q_zu_approval_default = N'1'
                )
            ORDER BY
                CASE WHEN q.perssign2 = @CurrentApproverPersSign THEN 0 ELSE 1 END,
                q.SQLIDENTITY;
            """;

        return _jeevesSqlExecutor.QueryFirstOrDefaultAsync<LegacyApprovalChainRule>(
            connectionString,
            sql,
            new
            {
                request.CompanyCode,
                request.PurchaseOrderNumber,
                request.FlowId,
                request.CurrentApproverPersSign
            },
            operationName: "ApprovalChainPurchaseJeevesReader.ReadLegacyRule",
            cancellationToken: cancellationToken);
    }

    private static bool ShouldForward(decimal absoluteOrderValue, string currentApproverPersSign, LegacyApprovalChainRule rule)
    {
        if (string.Equals(rule.NextApproverPersSign, currentApproverPersSign, StringComparison.OrdinalIgnoreCase))
            return false;

        return rule.Limit != 0 && rule.Limit <= absoluteOrderValue;
    }

    private sealed record LegacyApprovalChainRule(
        int RuleSqlIdentity,
        string NextApproverPersSign,
        decimal Limit,
        decimal NegativeLimit,
        bool UsedDefaultRule,
        bool SendMail);
}

public interface IApprovalChainPurchaseJeevesReader
{
    Task<ApprovalChainPurchaseOrderSnapshot?> GetOrderAsync(
        string connectionString,
        short companyCode,
        long purchaseOrderNumber,
        CancellationToken cancellationToken = default);

    Task<ApprovalChainPurchaseDecision> EvaluateLegacyDecisionAsync(
        string connectionString,
        ApprovalChainPurchaseParityRequest request,
        ApprovalChainPurchaseOrderSnapshot order,
        CancellationToken cancellationToken = default);
}

public sealed record ApprovalChainPurchaseOrderSnapshot(
    short CompanyCode,
    long PurchaseOrderNumber,
    short PurchaseOrderType,
    decimal OrderValue);

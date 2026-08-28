using WebApp.Services.Admin.ApprovalChains;

namespace WebApp.Tests;

// Checks that purchase approval parity reports differences before Jeeves procedures are replaced.
public sealed class ApprovalChainPurchaseParityServiceTests
{
    [Fact]
    public async Task CompareAsync_Returns_Match_When_Portal_And_Jeeves_Decisions_Are_Equal()
    {
        var order = new ApprovalChainPurchaseOrderSnapshot(9900, 100092, 0, 125_000m);
        var decision = CreateDecision(
            ApprovalChainDecisionKind.ForwardToNextApprover,
            nextApprover: "zuaek",
            ruleSqlIdentity: 1,
            usedDefaultRule: true);

        var service = new ApprovalChainPurchaseParityService(
            new FakePortalDecisionService(decision),
            new FakeJeevesReader(order, decision));

        var result = await service.CompareAsync(CreateRequest());

        Assert.True(result.Matches);
        Assert.Empty(result.Differences);
        Assert.Equal(order, result.Order);
    }

    [Fact]
    public async Task CompareAsync_Returns_Differences_When_Decisions_Do_Not_Match()
    {
        var order = new ApprovalChainPurchaseOrderSnapshot(9900, 100092, 0, 125_000m);
        var portalDecision = CreateDecision(
            ApprovalChainDecisionKind.FinalApprove,
            nextApprover: "zuaek",
            ruleSqlIdentity: 2,
            usedDefaultRule: false);
        var jeevesDecision = CreateDecision(
            ApprovalChainDecisionKind.ForwardToNextApprover,
            nextApprover: "JIS",
            ruleSqlIdentity: 1,
            usedDefaultRule: true);

        var service = new ApprovalChainPurchaseParityService(
            new FakePortalDecisionService(portalDecision),
            new FakeJeevesReader(order, jeevesDecision));

        var result = await service.CompareAsync(CreateRequest());

        Assert.False(result.Matches);
        Assert.Contains(result.Differences, difference => difference.StartsWith("Kind:", StringComparison.Ordinal));
        Assert.Contains(result.Differences, difference => difference.StartsWith("NextApproverPersSign:", StringComparison.Ordinal));
        Assert.Contains(result.Differences, difference => difference.StartsWith("UsedDefaultRule:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CompareAsync_Returns_No_Match_When_Order_Is_Missing()
    {
        var service = new ApprovalChainPurchaseParityService(
            new FakePortalDecisionService(CreateDecision(ApprovalChainDecisionKind.FinalApprove, "zuaek", 1, false)),
            new FakeJeevesReader(null, CreateDecision(ApprovalChainDecisionKind.FinalApprove, "zuaek", 1, false)));

        var result = await service.CompareAsync(CreateRequest());

        Assert.False(result.Matches);
        Assert.Null(result.Order);
        Assert.Contains("Purchase order was not found in Jeeves.", result.Differences);
    }

    private static ApprovalChainPurchaseParityRequest CreateRequest()
        => new(
            "Server=.;Database=Jeeves_2024;Trusted_Connection=True;",
            9900,
            100092,
            0,
            "JIS");

    private static ApprovalChainPurchaseDecision CreateDecision(
        ApprovalChainDecisionKind kind,
        string nextApprover,
        int ruleSqlIdentity,
        bool usedDefaultRule)
        => new(
            kind,
            nextApprover,
            1_000m,
            -1_000m,
            ruleSqlIdentity,
            usedDefaultRule,
            false,
            "Test decision.");

    private sealed class FakePortalDecisionService : IApprovalChainPurchaseDecisionService
    {
        private readonly ApprovalChainPurchaseDecision _decision;

        public FakePortalDecisionService(ApprovalChainPurchaseDecision decision)
        {
            _decision = decision;
        }

        public Task<ApprovalChainPurchaseDecision> EvaluateAsync(
            ApprovalChainPurchaseDecisionRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_decision);
    }

    private sealed class FakeJeevesReader : IApprovalChainPurchaseJeevesReader
    {
        private readonly ApprovalChainPurchaseOrderSnapshot? _order;
        private readonly ApprovalChainPurchaseDecision _decision;

        public FakeJeevesReader(
            ApprovalChainPurchaseOrderSnapshot? order,
            ApprovalChainPurchaseDecision decision)
        {
            _order = order;
            _decision = decision;
        }

        public Task<ApprovalChainPurchaseOrderSnapshot?> GetOrderAsync(
            string connectionString,
            short companyCode,
            long purchaseOrderNumber,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_order);

        public Task<ApprovalChainPurchaseDecision> EvaluateLegacyDecisionAsync(
            string connectionString,
            ApprovalChainPurchaseParityRequest request,
            ApprovalChainPurchaseOrderSnapshot order,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_decision);
    }
}

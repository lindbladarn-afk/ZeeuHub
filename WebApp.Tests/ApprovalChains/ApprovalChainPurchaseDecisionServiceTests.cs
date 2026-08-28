using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Admin.ApprovalChains;
using WebApp.Services.Admin.ApprovalChains;

namespace WebApp.Tests;

// Covers dry-run purchase approval decisions against portal-owned approval-chain rows.
public sealed class ApprovalChainPurchaseDecisionServiceTests
{
    [Fact]
    public async Task EvaluateAsync_Uses_Default_Rule_When_Current_Approver_Has_No_Row()
    {
        await using var context = CreateContext();
        context.ApprovalChainRules!.Add(CreateRule(
            sqlIdentity: 1,
            current: "",
            next: "zuaek",
            limit: 1_000m,
            isDefault: "1"));
        await context.SaveChangesAsync();

        var service = new ApprovalChainPurchaseDecisionService(context);

        var decision = await service.EvaluateAsync(new ApprovalChainPurchaseDecisionRequest(
            CompanyCode: 9900,
            FlowId: 0,
            CurrentApproverPersSign: "JIS",
            PurchaseOrderType: 0,
            OrderValue: 5_000m));

        Assert.Equal(ApprovalChainDecisionKind.ForwardToNextApprover, decision.Kind);
        Assert.Equal("zuaek", decision.NextApproverPersSign);
        Assert.True(decision.UsedDefaultRule);
    }

    [Fact]
    public async Task EvaluateAsync_FinalApproves_When_Next_Approver_Is_Current_Approver()
    {
        await using var context = CreateContext();
        context.ApprovalChainRules!.Add(CreateRule(
            sqlIdentity: 2,
            current: "zuaek",
            next: "zuaek",
            limit: 100_000m,
            isDefault: null));
        await context.SaveChangesAsync();

        var service = new ApprovalChainPurchaseDecisionService(context);

        var decision = await service.EvaluateAsync(new ApprovalChainPurchaseDecisionRequest(
            CompanyCode: 9900,
            FlowId: 0,
            CurrentApproverPersSign: "zuaek",
            PurchaseOrderType: 0,
            OrderValue: 150_000m));

        Assert.Equal(ApprovalChainDecisionKind.FinalApprove, decision.Kind);
        Assert.Equal("zuaek", decision.NextApproverPersSign);
        Assert.False(decision.UsedDefaultRule);
    }

    [Fact]
    public async Task EvaluateAsync_Returns_NoRule_When_No_Rule_Matches()
    {
        await using var context = CreateContext();
        var service = new ApprovalChainPurchaseDecisionService(context);

        var decision = await service.EvaluateAsync(new ApprovalChainPurchaseDecisionRequest(
            CompanyCode: 9900,
            FlowId: 0,
            CurrentApproverPersSign: "JIS",
            PurchaseOrderType: 0,
            OrderValue: 5_000m));

        Assert.Equal(ApprovalChainDecisionKind.NoRule, decision.Kind);
        Assert.Null(decision.NextApproverPersSign);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ApprovalChainRuleRecord CreateRule(
        int sqlIdentity,
        string current,
        string next,
        decimal limit,
        string? isDefault)
    {
        return new ApprovalChainRuleRecord
        {
            ForetagKod = 9900,
            SqlIdentity = sqlIdentity,
            FlowId = 0,
            CurrentApproverPersSign = current,
            NextApproverPersSign = next,
            PurchaseOrderType = 0,
            Limit = limit,
            NegativeLimit = -limit,
            RegisteredAt = DateTime.UtcNow,
            PersSign = "JIS",
            RowCreatedBy = "JIS",
            RowCreatedAt = DateTime.UtcNow,
            IsDefaultRaw = isDefault
        };
    }
}

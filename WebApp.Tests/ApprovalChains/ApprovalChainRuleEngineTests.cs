using WebApp.Services.Admin.ApprovalChains;

namespace WebApp.Tests;

// Verifies the portal approval rules independently from the UI.
public sealed class ApprovalChainRuleEngineTests
{
    [Fact]
    public void ResolveActiveStep_Selects_The_First_Step_That_Covers_The_Amount()
    {
        var engine = new ApprovalChainRuleEngine();
        var purchase = ApprovalChainCatalog.GetOrderTypes().Single(x => x.Key == "purchase");
        var model = ApprovalChainCatalog.MapOrderType(purchase);

        var activeStep = engine.ResolveActiveStep(model, 25_000m);

        Assert.NotNull(activeStep);
        Assert.Equal(1, activeStep!.Sequence);
        Assert.Equal("Anna Lindström", activeStep.ApproverName);
    }

    [Fact]
    public void ResolvePath_Stops_On_The_Final_Unlimited_Step()
    {
        var engine = new ApprovalChainRuleEngine();
        var purchase = ApprovalChainCatalog.GetOrderTypes().Single(x => x.Key == "purchase");
        var model = ApprovalChainCatalog.MapOrderType(purchase);

        var path = engine.ResolvePath(model, 150_000m);

        Assert.Equal(3, path.Count);
        Assert.Equal("CFO", path[^1].RoleName);
        Assert.True(path[^1].Limit is null);
    }

    [Fact]
    public void ResolveActiveStep_Uses_Negative_Limit_For_Credit_Amounts()
    {
        var engine = new ApprovalChainRuleEngine();
        var purchase = ApprovalChainCatalog.GetOrderTypes().Single(x => x.Key == "purchase");
        var model = ApprovalChainCatalog.MapOrderType(purchase);

        var activeStep = engine.ResolveActiveStep(model, -30_000m);

        Assert.NotNull(activeStep);
        Assert.Equal(2, activeStep!.Sequence);
        Assert.Equal("Marcus Ek", activeStep.ApproverName);
    }
}

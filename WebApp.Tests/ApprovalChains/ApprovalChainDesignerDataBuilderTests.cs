using WebApp.Services.Admin.ApprovalChains;

namespace WebApp.Tests;

public sealed class ApprovalChainDesignerDataBuilderTests
{
    [Fact]
    public void Build_Returns_A_Complete_Approval_Designer_Model()
    {
        var model = ApprovalChainDesignerDataBuilder.Build();

        Assert.Equal("Attestkedja", model.PageTitle);
        Assert.Equal("purchase", model.SelectedOrderTypeKey);
        Assert.Equal(5, model.OrderTypes.Count);
        Assert.Equal(4, model.RolePresets.Count);
        Assert.Equal(7, model.ApproverOptions.Count);

        var purchase = Assert.Single(model.OrderTypes, x => string.Equals(x.Key, "purchase", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, purchase.Steps.Count);
        Assert.Equal(4, purchase.QuickAmounts.Count);
        Assert.True(purchase.Steps[^1].Limit is null);
    }
}

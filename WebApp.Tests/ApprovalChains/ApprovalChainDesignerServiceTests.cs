using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Data;
using WebApp.Models.Admin.ApprovalChains;
using WebApp.Services.Admin.ApprovalChains;

namespace WebApp.Tests;

// Verifies that portal-owned approval-chain rows are shaped into the designer model.
public sealed class ApprovalChainDesignerServiceTests
{
    [Fact]
    public async Task BuildAsync_Uses_Portal_Rules_When_They_Exist()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var context = new ApplicationDbContext(options);
        context.ApprovalChainRules!.AddRange(
            CreateRule(sqlIdentity: 1, current: "", next: "zuaek", limit: 1_000m, isDefault: "1"),
            CreateRule(sqlIdentity: 2, current: "zuaek", next: "zuaek", limit: 100_000m, isDefault: null));
        await context.SaveChangesAsync();

        var service = new ApprovalChainDesignerService(context, NullLogger<ApprovalChainDesignerService>.Instance);

        var model = await service.BuildAsync(9900);

        var purchase = Assert.Single(model.OrderTypes);
        Assert.Equal("purchase-0", purchase.Key);
        Assert.Equal("Inköp / Material (0)", purchase.Title);
        Assert.Equal(2, purchase.Steps.Count);
        Assert.Equal("zuaek", purchase.Steps[0].ApproverName);
        Assert.Equal(1_000m, purchase.Steps[0].Limit);
        Assert.Equal(-1_000m, purchase.Steps[0].NegativeLimit);
        Assert.Equal("Startregel för ärenden utan tidigare attestant, skickar inte mail.", purchase.Steps[0].Note);
        Assert.DoesNotContain("SQLIDENTITY", purchase.Steps[0].Note, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(model.ApproverOptions, option => option.Key == "zuaek");
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

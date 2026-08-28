// Verifies that WebApproval failure reports identify the procedure used by each module.
using System.Reflection;
using WebApp.Controllers;

namespace WebApp.Tests;

public sealed class WebApprovalStoredProcedureMetadataTests
{
    [Theory]
    [InlineData("SalesApprovalStoredProcedure", "q_zu_CustomerPortal_WebApprovalSales")]
    [InlineData("PurchaseApprovalStoredProcedure", "q_zu_CustomerPortal_WebApprovalPurchase")]
    public void ErrorMetadata_UsesModuleSpecificStoredProcedure(string fieldName, string expectedProcedure)
    {
        var field = typeof(WebApprovalController).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        Assert.Equal(expectedProcedure, field!.GetRawConstantValue());
    }
}

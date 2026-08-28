namespace WebApp.Tests;

// Guards the Jeeves purchase approval SQL contract used by the approval detail page.
public sealed class WebApprovalPurchaseSqlTests
{
    [Fact]
    public void PurchaseApprovalDetails_Query_Uses_Only_Primary_PurchaseRows()
    {
        var sqlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "SQL",
            "JeevesDb",
            "StoredProcedures",
            "q_zu_CustomerPortal_WebApprovalPurchase.sql"));
        var sql = File.ReadAllText(sqlPath);

        Assert.Contains("AND bp.BestRestNr=0", sql, StringComparison.OrdinalIgnoreCase);
    }
}

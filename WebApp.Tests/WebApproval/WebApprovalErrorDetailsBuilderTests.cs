using WebApp.Services.WebApproval;

namespace WebApp.Tests;

// Verifies the shared WebApproval error formatting used by the approval controllers.
public sealed class WebApprovalErrorDetailsBuilderTests
{
    [Fact]
    public void BuildExceptionDetails_Includes_Operation_And_Extra_Lines()
    {
        var details = WebApprovalErrorDetailsBuilder.BuildExceptionDetails(
            "Boom",
            "q_zu_CustomerPortal_WebApprovalSales",
            "Id 123",
            "Status 1");

        Assert.Contains("Boom", details);
        Assert.Contains("Original StoredProcedure: q_zu_CustomerPortal_WebApprovalSales", details);
        Assert.Contains("Id 123", details);
        Assert.Contains("Status 1", details);
    }

    [Fact]
    public void BuildSqlErrorDetails_Includes_Sql_Metadata_And_Extra_Lines()
    {
        var exception = new FakeSqlException("Invalid object name 'dbo.q_zu_notcenter'.")
        {
            Procedure = "q_zu_notcenter",
            LineNumber = 18,
            Number = 208
        };

        var details = WebApprovalErrorDetailsBuilder.BuildSqlErrorDetails(
            "fetching data in PriceListApproval",
            exception,
            "q_zu_CustomerPortal_WebApprovalPriceList",
            "ForetagKod 123",
            "PersSign2 ABC");

        Assert.Contains("SQL Error when fetching data in PriceListApproval:", details);
        Assert.Contains("Procedure=q_zu_notcenter", details);
        Assert.Contains("LineNumber=18", details);
        Assert.Contains("Number=208", details);
        Assert.Contains("Original StoredProcedure: q_zu_CustomerPortal_WebApprovalPriceList", details);
        Assert.Contains("ForetagKod 123", details);
        Assert.Contains("PersSign2 ABC", details);
    }

    private sealed class FakeSqlException : Exception
    {
        public FakeSqlException(string message) : base(message)
        {
        }

        public string? Procedure { get; set; }
        public int LineNumber { get; set; }
        public int Number { get; set; }
    }
}

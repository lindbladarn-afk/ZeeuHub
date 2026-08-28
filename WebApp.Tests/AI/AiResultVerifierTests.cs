// Verifies evidence status against the actual numeric SQL result.
using WebApp.Models.AI;
using WebApp.Services.Application.AI;

namespace WebApp.Tests;

public sealed class AiResultVerifierTests
{
    private readonly AiResultVerifier _verifier = new();

    [Fact]
    public void Verify_MarksMatchingNumericClaimAsVerified()
    {
        var query = Result(1250.50m);

        var evidence = _verifier.Verify(
            "Omsättningen är 1 250,50 kr.",
            query,
            new AiQueryPlan { Metric = "net_revenue", Period = "current_year" },
            "Aktiv tenant",
            "Omsättning",
            "SELECT SUM(sf.Amount) AS Revenue FROM dbo.SalesFact sf");

        Assert.Equal("verified", evidence.VerificationStatus);
        Assert.Equal("Omsättning", evidence.MetricLabel);
        Assert.Contains("dbo.SalesFact", evidence.SourceTables);
    }

    [Fact]
    public void Verify_FlagsNumericClaimMissingFromResult()
    {
        var evidence = _verifier.Verify(
            "Omsättningen är 9 999 kr.",
            Result(1250m),
            null,
            "Aktiv tenant",
            null,
            "SELECT SUM(sf.Amount) AS Revenue FROM dbo.SalesFact sf");

        Assert.Equal("needs_review", evidence.VerificationStatus);
        Assert.NotEmpty(evidence.Notes);
    }

    private static SqlQueryResult Result(decimal amount)
    {
        var result = new SqlQueryResult
        {
            Success = true,
            RowCount = 1
        };
        result.Columns.Add("Revenue");
        result.Rows.Add([amount]);
        return result;
    }
}

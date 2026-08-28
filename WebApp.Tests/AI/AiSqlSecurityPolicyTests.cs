// Verifies that Intelligence permits flexible reads inside the selected database only.
using WebApp.Services.Application.AI;

namespace WebApp.Tests;

public sealed class AiSqlSecurityPolicyTests
{
    private readonly AiSqlSecurityPolicy _policy = new();

    [Fact]
    public void Validate_AllowsReadWithoutCompanyFilter()
    {
        var result = _policy.Validate(
            "SELECT SUM(sf.Amount) AS Total FROM dbo.SalesFact sf");

        Assert.True(result.Success);
    }

    [Fact]
    public void Validate_AllowsAnyTableInsideSelectedDatabase()
    {
        var result = _policy.Validate(
            "SELECT TOP (10) * FROM dbo.CustomBusinessView");

        Assert.True(result.Success);
    }

    [Fact]
    public void Validate_AllowsFullyQualifiedColumns()
    {
        var result = _policy.Validate(
            """
            SELECT TOP (1)
                [dbo].[q_zu_bi_fsg].[Invoice No] AS InvoiceNo,
                [dbo].[q_zu_bi_fsg].[Invoice Row SUM] AS Amount
            FROM [dbo].[q_zu_bi_fsg]
            ORDER BY [dbo].[q_zu_bi_fsg].[Invoice Row SUM] DESC
            """);

        Assert.True(result.Success);
    }

    [Fact]
    public void Validate_AllowsJoinsWithoutBusinessRuleInspection()
    {
        var result = _policy.Validate(
            """
            SELECT c.Name, SUM(sf.Amount) AS Total
            FROM dbo.SalesFact sf
            LEFT JOIN dbo.Customer c ON sf.CustomerId = c.CustomerId
            GROUP BY c.Name
            """);

        Assert.True(result.Success);
    }

    [Theory]
    [InlineData("SELECT * FROM OPENROWSET('x', 'y', 'z')")]
    [InlineData("SELECT * FROM OPENQUERY(RemoteServer, 'SELECT 1')")]
    [InlineData("SELECT 1; WAITFOR DELAY '00:00:01'")]
    public void Validate_RejectsExternalOrServerLevelReads(string sql)
    {
        var result = _policy.Validate(sql);

        Assert.False(result.Success);
        Assert.Equal("external_data_source", result.ErrorCode);
    }

    [Fact]
    public void Validate_RejectsCrossDatabaseTableReference()
    {
        var result = _policy.Validate(
            "SELECT * FROM [OtherDatabase].[dbo].[Secret]");

        Assert.False(result.Success);
        Assert.Equal("cross_database_access", result.ErrorCode);
    }

    [Fact]
    public async Task Executor_RejectsSelectIntoBeforeOpeningConnection()
    {
        var executor = new AiSqlExecutor();

        var result = await executor.ExecuteSelectAsync(
            "Server=unused;Database=unused;User Id=unused;Password=unused;",
            "SELECT * INTO dbo.CopiedData FROM dbo.SalesFact");

        Assert.False(result.Success);
        Assert.Contains("forbidden", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}

using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

// Verifies shared Excel Import result shaping before controllers render it.
public sealed class ExcelImportResultFactoryTests
{
    [Fact]
    public void Create_Sets_Import_Type_And_Copies_Collections()
    {
        var headers = new List<string> { "Account" };
        var errors = new List<string> { "Rad 2: Amount saknas." };
        var rows = new List<ExcelImportRowResult>
        {
            new()
            {
                RowNo = 2,
                IsValid = false,
                Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            }
        };
        var factory = new ExcelImportResultFactory();

        var result = factory.Create(new ExcelImportResultCreateRequest
        {
            ImportType = "budget",
            ImportBatchId = Guid.NewGuid(),
            EditSessionId = Guid.NewGuid(),
            TotalRows = 1,
            ValidRows = 0,
            InvalidRows = 1,
            RowHeaders = headers,
            RowResults = rows,
            Errors = errors
        });

        headers.Add("Amount");
        errors.Clear();
        rows.Clear();

        Assert.Equal("budget", result.ImportType);
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.ValidRows);
        Assert.Equal(1, result.InvalidRows);
        Assert.Equal(["Account"], result.RowHeaders);
        Assert.Single(result.RowResults);
        Assert.Equal(["Rad 2: Amount saknas."], result.Errors);
    }
}

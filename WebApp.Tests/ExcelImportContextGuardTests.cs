using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

// Verifies staging cannot continue without complete tenant and user ownership.
public sealed class ExcelImportContextGuardTests
{
    [Fact]
    public void GetRequiredCurrent_ReturnsCompleteContext()
    {
        var expected = new ExcelImportUserContext
        {
            CompanyId = Guid.NewGuid(),
            ForetagKod = 10,
            UserId = "user-1"
        };

        var actual = ExcelImportContextGuard.GetRequiredCurrent(new StaticContextService(expected));

        Assert.Same(expected, actual);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void GetRequiredCurrent_RejectsIncompleteContext(
        bool missingCompany,
        bool missingCompanyCode,
        bool missingUser)
    {
        var context = new ExcelImportUserContext
        {
            CompanyId = missingCompany ? null : Guid.NewGuid(),
            ForetagKod = missingCompanyCode ? null : 10,
            UserId = missingUser ? null : "user-1"
        };

        Assert.Throws<InvalidOperationException>(() =>
            ExcelImportContextGuard.GetRequiredCurrent(new StaticContextService(context)));
    }

    private sealed class StaticContextService : IExcelImportContextService
    {
        private readonly ExcelImportUserContext _context;

        public StaticContextService(ExcelImportUserContext context)
        {
            _context = context;
        }

        public ExcelImportUserContext GetCurrent() => _context;
    }
}

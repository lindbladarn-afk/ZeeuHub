using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

// Verifies the shared edit-session JSON contract matches browser-generated camelCase payloads.
public sealed class ExcelImportEditSessionAdapterJsonTests
{
    [Fact]
    public async Task ImportEditedRowsAsync_Deserializes_CamelCase_RowJson()
    {
        var adapter = new TestAdapter();
        var json = """
            [
              {
                "rowNo": 1,
                "data": {
                  "Account": "6550",
                  "Debit": "100,00",
                  "Credit": ""
                }
              }
            ]
            """;

        var result = await adapter.ImportEditedRowsAsync(
            Guid.NewGuid(),
            json,
            "tester",
            new ExcelImportEditSessionContext());

        Assert.True(adapter.CapturedRows is not null);
        Assert.Single(adapter.CapturedRows);
        Assert.Equal(1, adapter.CapturedRows[0].RowNo);
        Assert.Equal("6550", adapter.CapturedRows[0].Data["Account"]);
        Assert.Equal("100,00", adapter.CapturedRows[0].Data["Debit"]);
        Assert.Equal("test", result.ImportType);
    }

    [Fact]
    public async Task ImportEditedRowsAsync_Rejects_Empty_RowJson()
    {
        var adapter = new TestAdapter();

        var ex = await Assert.ThrowsAsync<InvalidExcelImportRowsException>(() =>
            adapter.ImportEditedRowsAsync(
                Guid.NewGuid(),
                "[]",
                "tester",
                new ExcelImportEditSessionContext()));

        Assert.Contains("Minst en rad", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestAdapter : ExcelImportEditSessionAdapterBase<TestRow>
    {
        public IReadOnlyList<TestRow>? CapturedRows { get; private set; }

        public override string ImportType => "test";
        public override string EditSessionFileName => "test.xlsx";
        public override int MaxEditableRows => 100;

        public override Task<ExcelImportResult> CreateEditSessionFromFileAsync(
            Microsoft.AspNetCore.Http.IFormFile file,
            string importedBy,
            ExcelImportEditSessionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ExcelImportResult.Empty(ImportType));

        public override Task<ExcelImportResult> CreateEmptyEditSessionAsync(
            string importedBy,
            ExcelImportEditSessionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ExcelImportResult.Empty(ImportType));

        protected override Task<ExcelImportResult> ImportRowsCoreAsync(
            Guid editSessionId,
            IReadOnlyList<TestRow> rows,
            string importedBy,
            ExcelImportEditSessionContext context,
            CancellationToken cancellationToken)
        {
            CapturedRows = rows;
            return Task.FromResult(ExcelImportResult.Empty(ImportType));
        }

        protected override TestRow CreateRow(ExcelImportRowResult row)
            => new()
            {
                RowNo = row.RowNo,
                Data = row.Data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
    }

    private sealed class TestRow
    {
        public int RowNo { get; set; }
        public Dictionary<string, string> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

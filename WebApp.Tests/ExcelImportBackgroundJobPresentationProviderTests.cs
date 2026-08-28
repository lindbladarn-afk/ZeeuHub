using WebApp.Models.BackgroundJobs;
using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

// Verifies background-job notifications surface the editable Excel Import flow.
public sealed class ExcelImportBackgroundJobPresentationProviderTests
{
    [Fact]
    public void CreateEvent_Completed_Uses_Recent_Edit_Link()
    {
        var provider = new ExcelImportBackgroundJobPresentationProvider();
        var job = new BackgroundJobSnapshot
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            JobType = ExcelImportBackgroundJobConstants.ExecuteJobType,
            StartedAtUtc = new DateTime(2026, 06, 11, 10, 00, 00, DateTimeKind.Utc),
            CompletedAtUtc = new DateTime(2026, 06, 11, 10, 02, 30, DateTimeKind.Utc),
            PayloadJson = new ExcelImportBackgroundJobPayload
            {
                ImportType = "budget",
                OriginalFileName = "budget.xlsx"
            }.ToJson()
        };
        var resultJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            ImportBatchId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            TotalRows = 4,
            ValidRows = 4,
            InvalidRows = 0,
            StagedRows = 4,
            ErrorCount = 0,
            FirstError = (string?)null,
            RowHeaders = new[] { "KolumnA", "KolumnB" },
            Rows = new[]
            {
                new
                {
                    RowNo = 1,
                    IsValid = true,
                    Data = new Dictionary<string, string>
                    {
                        ["KolumnA"] = "A1",
                        ["KolumnB"] = "B1"
                    }
                }
            }
        });

        var record = provider.CreateEvent(job, BackgroundJobStatus.Completed, resultJson, null);

        Assert.StartsWith("/ExcelImport/EditRecentImport?aggregateKey=", record.LinkUrl);
        Assert.Equal("Import klar", record.Title);
        Assert.Equal("budget.xlsx", record.SourceFileName);
        Assert.Equal(job.StartedAtUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(job.StartedAtUtc.Value, DateTimeKind.Utc)) : null, record.StartedAtUtc);
        Assert.Equal(Guid.Parse("11111111-2222-3333-4444-555555555555"), record.ImportBatchId);
        Assert.Equal(4, record.TotalRows);
        Assert.Equal(4, record.ValidRows);
        Assert.Equal(0, record.InvalidRows);
        Assert.Equal(4, record.StagedRows);
        Assert.Equal("Körtid: 00:02:30.000", record.DurationLabel);
        Assert.Equal(2, record.ColumnHeaders.Count);
        Assert.Single(record.ImportedRows);
        Assert.Equal("A1", record.ImportedRows[0].Cells["KolumnA"]);
    }

    [Fact]
    public void CreateEvent_NonCompleted_Does_Not_Expose_Edit_Link()
    {
        var provider = new ExcelImportBackgroundJobPresentationProvider();
        var job = new BackgroundJobSnapshot
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            JobType = ExcelImportBackgroundJobConstants.ExecuteJobType,
            StartedAtUtc = new DateTime(2026, 06, 11, 10, 00, 00, DateTimeKind.Utc),
            CompletedAtUtc = new DateTime(2026, 06, 11, 10, 01, 15, DateTimeKind.Utc),
            PayloadJson = new ExcelImportBackgroundJobPayload
            {
                ImportType = "budget",
                OriginalFileName = "budget.xlsx"
            }.ToJson()
        };

        var record = provider.CreateEvent(job, BackgroundJobStatus.Running, null, null);

        Assert.Null(record.LinkUrl);
    }

    [Fact]
    public void CreateEvent_Failed_Summary_Includes_ImportType()
    {
        var provider = new ExcelImportBackgroundJobPresentationProvider();
        var job = new BackgroundJobSnapshot
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            JobType = ExcelImportBackgroundJobConstants.ExecuteJobType,
            PayloadJson = new ExcelImportBackgroundJobPayload
            {
                ImportType = "voucher",
                OriginalFileName = "vouchers.xlsx"
            }.ToJson()
        };

        var record = provider.CreateEvent(job, BackgroundJobStatus.Failed, null, "boom");

        Assert.Contains("Voucher-import", record.Summary);
    }

    [Fact]
    public void CreateEvent_Failed_With_Row_Data_Exposes_Correction_Link()
    {
        var provider = new ExcelImportBackgroundJobPresentationProvider();
        var job = new BackgroundJobSnapshot
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            JobType = ExcelImportBackgroundJobConstants.ExecuteJobType,
            StartedAtUtc = new DateTime(2026, 06, 11, 10, 00, 00, DateTimeKind.Utc),
            CompletedAtUtc = new DateTime(2026, 06, 11, 10, 01, 15, DateTimeKind.Utc),
            PayloadJson = new ExcelImportBackgroundJobPayload
            {
                ImportType = "budget",
                OriginalFileName = "budget.xlsx"
            }.ToJson()
        };
        var resultJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            TotalRows = 1,
            ValidRows = 0,
            InvalidRows = 1,
            StagedRows = 0,
            FirstError = "Rad 2: Amount saknas.",
            RowHeaders = new[] { "Account", "Amount" },
            Rows = new[]
            {
                new
                {
                    RowNo = 2,
                    IsValid = false,
                    ErrorMessage = "Amount måste anges.",
                    Data = new Dictionary<string, string>
                    {
                        ["Account"] = "6110",
                        ["Amount"] = ""
                    }
                }
            }
        });

        var record = provider.CreateEvent(job, BackgroundJobStatus.Failed, resultJson, null);

        Assert.StartsWith("/ExcelImport/EditRecentImport?aggregateKey=", record.LinkUrl);
        Assert.Equal("budget.xlsx", record.SourceFileName);
        Assert.Equal(job.StartedAtUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(job.StartedAtUtc.Value, DateTimeKind.Utc)) : null, record.StartedAtUtc);
        Assert.Equal(1, record.TotalRows);
        Assert.Equal(0, record.ValidRows);
        Assert.Equal(1, record.InvalidRows);
        Assert.Equal(0, record.StagedRows);
        Assert.Equal("Körtid: 00:01:15.000", record.DurationLabel);
        var invalidRow = Assert.Single(record.ImportedRows);
        Assert.Equal("Amount måste anges.", invalidRow.ErrorMessage);
    }
}

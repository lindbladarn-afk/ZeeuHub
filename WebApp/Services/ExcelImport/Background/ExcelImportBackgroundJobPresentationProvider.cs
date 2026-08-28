using System.Text.Json;
using WebApp.Models.Application;
using WebApp.Models.BackgroundJobs;
using WebApp.Services.Application.BackgroundJobs;

namespace WebApp.Services.ExcelImport;

// Describes how Excel import background jobs appear in the shared runtime status menu.
public sealed class ExcelImportBackgroundJobPresentationProvider : IBackgroundJobPresentationProvider
{
    public string JobType => ExcelImportBackgroundJobConstants.ExecuteJobType;

    public SidebarRuntimeEventRecord CreateEvent(
        BackgroundJobSnapshot job,
        BackgroundJobStatus status,
        string? resultJson,
        string? errorMessage)
    {
        var payload = ExcelImportBackgroundJobPayload.FromJson(job.PayloadJson);
        var result = ExcelImportBackgroundJobResultSummary.FromJson(resultJson);
        var fileName = string.IsNullOrWhiteSpace(payload.OriginalFileName) ? "filen" : payload.OriginalFileName;
        var importType = ExcelImportTypeDefinitions.Get(payload.ImportType);
        var aggregateKey = $"excel-import:{importType.ImportType}:{job.Id:N}";
        var hasEditableRows = result?.Rows is { Count: > 0 };
        var resultLink = (status == BackgroundJobStatus.Completed || (status == BackgroundJobStatus.Failed && hasEditableRows))
            ? $"/ExcelImport/EditRecentImport?aggregateKey={Uri.EscapeDataString(aggregateKey)}&scrollTarget=excel-edit-table"
            : null;

        var (statusLabel, statusTone, title, summary) = status switch
        {
            BackgroundJobStatus.Queued => ("Queued", "info", "Import köad", $"{importType.DisplayName}: {fileName} ligger i kö och startar strax."),
            BackgroundJobStatus.Running => ("Running", "info", "Import pågår", $"{importType.DisplayName}: {fileName} läses, valideras och importeras."),
            BackgroundJobStatus.Completed => ("Completed", "success", "Import klar", BuildCompletedSummary(payload, result)),
            BackgroundJobStatus.Failed => ("Failed", "danger", "Excelimport kräver åtgärd", BuildFailedSummary(payload, fileName, result, errorMessage)),
            BackgroundJobStatus.Canceled => ("Canceled", "muted", "Excelimport avbruten", $"Import av {fileName} avbröts."),
            _ => (status.ToString(), "muted", "Excelimport uppdaterad", $"Import av {fileName} uppdaterades.")
        };

        return new SidebarRuntimeEventRecord
        {
            CompanyId = job.CompanyId,
            AggregateKey = aggregateKey,
            ImportBatchId = result?.ImportBatchId,
            SourceFileName = fileName,
            StartedAtUtc = job.StartedAtUtc.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(job.StartedAtUtc.Value, DateTimeKind.Utc))
                : null,
            TotalRows = result?.TotalRows,
            ValidRows = result?.ValidRows,
            InvalidRows = result?.InvalidRows,
            StagedRows = result?.StagedRows,
            DurationLabel = BuildDurationLabel(job, status),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Source = "ExcelImport",
            Title = title,
            Summary = summary,
            LinkUrl = resultLink,
            StatusLabel = statusLabel,
            StatusTone = statusTone,
            IconClass = "fas fa-file-excel",
            ColumnHeaders = result?.RowHeaders?.ToList() ?? new List<string>(),
            ImportedRows = MapRows(result),
            VoucherPostingDate = payload.VoucherPostingDate,
            VoucherReversalDate = payload.VoucherReversalDate
        };
    }

    private static string BuildCompletedSummary(ExcelImportBackgroundJobPayload payload, ExcelImportBackgroundJobResultSummary? result)
    {
        if (result is null)
            return $"{ExcelImportTypeDefinitions.Get(payload.ImportType).DisplayName} importerades.";

        if (result.InvalidRows > 0)
            return $"{ExcelImportTypeDefinitions.Get(payload.ImportType).DisplayName}: importen är klar. {result.StagedRows} av {result.TotalRows} rader importerades. {result.InvalidRows} rader hade fel.";

        return $"{ExcelImportTypeDefinitions.Get(payload.ImportType).DisplayName}: importen är klar. {result.StagedRows} av {result.TotalRows} rader importerades.";
    }

    private static string BuildFailedSummary(ExcelImportBackgroundJobPayload payload, string fileName, ExcelImportBackgroundJobResultSummary? result, string? errorMessage)
    {
        var typeLabel = ExcelImportTypeDefinitions.Get(payload.ImportType).DisplayName;

        if (!string.IsNullOrWhiteSpace(result?.FirstError))
            return $"{typeLabel}-import av {fileName} stoppades: {result.FirstError}";

        if (!string.IsNullOrWhiteSpace(errorMessage))
            return $"{typeLabel}-import av {fileName} misslyckades: {errorMessage}";

        return $"{typeLabel}-import av {fileName} misslyckades.";
    }

    private static List<ExcelImportRuntimeRowViewModel> MapRows(ExcelImportBackgroundJobResultSummary? result)
    {
        if (result?.Rows is null || result.Rows.Count == 0)
            return new List<ExcelImportRuntimeRowViewModel>();

        return result.Rows
            .OrderBy(row => row.RowNo)
            .Select(row => new ExcelImportRuntimeRowViewModel
            {
                RowNo = row.RowNo,
                IsValid = row.IsValid,
                ErrorMessage = row.ErrorMessage,
                Cells = row.Data is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(row.Data, StringComparer.OrdinalIgnoreCase)
            })
            .ToList();
    }

    private static string? BuildDurationLabel(BackgroundJobSnapshot job, BackgroundJobStatus status)
    {
        var startedAtUtc = job.StartedAtUtc;
        var endedAtUtc = job.CompletedAtUtc;

        if (status == BackgroundJobStatus.Running && startedAtUtc.HasValue)
        {
            var elapsed = DateTime.UtcNow - startedAtUtc.Value;
            return elapsed < TimeSpan.Zero ? null : $"Körtid: {FormatDuration(elapsed)}";
        }

        if (endedAtUtc.HasValue && startedAtUtc.HasValue)
        {
            var elapsed = endedAtUtc.Value - startedAtUtc.Value;
            return elapsed < TimeSpan.Zero ? null : $"Körtid: {FormatDuration(elapsed)}";
        }

        return null;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var totalHours = (int)duration.TotalHours;
        var minutes = duration.Minutes;
        var seconds = duration.Seconds;
        var milliseconds = duration.Milliseconds;
        return $"{totalHours:D2}:{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
    }

    private sealed class ExcelImportBackgroundJobResultSummary
    {
        public Guid ImportBatchId { get; set; }
        public int TotalRows { get; set; }
        public int ValidRows { get; set; }
        public int InvalidRows { get; set; }
        public int StagedRows { get; set; }
        public string? FirstError { get; set; }
        public List<string> RowHeaders { get; set; } = new();
        public List<ExcelImportBackgroundJobRowSummary> Rows { get; set; } = new();

        public static ExcelImportBackgroundJobResultSummary? FromJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<ExcelImportBackgroundJobResultSummary>(
                    json,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch
            {
                return null;
            }
        }
    }

    private sealed class ExcelImportBackgroundJobRowSummary
    {
        public int RowNo { get; set; }
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, string> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

using System.Text.Json;

namespace WebApp.Services.ExcelImport;

// Carries the minimum data needed to run an uploaded Excel import outside the request.
public sealed class ExcelImportBackgroundJobPayload
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ImportType { get; set; } = string.Empty;
    public string ImportedBy { get; set; } = string.Empty;
    public string? CreatedByUserId { get; set; }
    public string? CreatedByEmail { get; set; }
    public Guid CompanyId { get; set; }
    public int? JeevesActiveCompany { get; set; }
    public string? VoucherPostingDate { get; set; }
    public string? VoucherReversalDate { get; set; }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static ExcelImportBackgroundJobPayload FromJson(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? new ExcelImportBackgroundJobPayload()
            : JsonSerializer.Deserialize<ExcelImportBackgroundJobPayload>(json, JsonOptions) ?? new ExcelImportBackgroundJobPayload();
}

using System.ComponentModel.DataAnnotations;

namespace WebApp.Models.Telemetry;

public class ExcelImportLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? CompanyId { get; set; }
    public string? UserId { get; set; }
    public string? FileName { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ImportType { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

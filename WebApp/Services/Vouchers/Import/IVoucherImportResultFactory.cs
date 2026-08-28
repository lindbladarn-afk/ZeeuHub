using WebApp.Models.Voucher;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Vouchers;

// Builds voucher import results with consistent metadata for the UI and API layers.
public interface IVoucherImportResultFactory
{
    VoucherImportResult CreateImportResult(VoucherImportResultCreateRequest request);
    ExcelImportResult CreateEditSessionResult(VoucherEditSessionResultCreateRequest request);
    ExcelImportResult ToExcelImportResult(VoucherImportResult result, string importType);
}

public sealed class VoucherImportResultCreateRequest
{
    public Guid ImportBatchId { get; init; }
    public int TotalRows { get; init; }
    public int ValidRows { get; init; }
    public int StagedRows { get; init; }
    public DateTime? PostingDate { get; init; }
    public DateTime? ReversalDate { get; init; }
    public IReadOnlyList<string> RowHeaders { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ExcelImportRowResult> RowResults { get; init; } = Array.Empty<ExcelImportRowResult>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed class VoucherEditSessionResultCreateRequest
{
    public Guid ImportBatchId { get; init; }
    public Guid? EditSessionId { get; init; }
    public int TotalRows { get; init; }
    public int ValidRows { get; init; }
    public int InvalidRows { get; init; }
    public int StagedRows { get; init; }
    public DateTime PostingDate { get; init; }
    public DateTime? ReversalDate { get; init; }
    public IReadOnlyList<string> RowHeaders { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ExcelImportRowResult> RowResults { get; init; } = Array.Empty<ExcelImportRowResult>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

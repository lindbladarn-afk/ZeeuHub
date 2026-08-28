using WebApp.Models.Voucher;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Vouchers;

// Centralizes voucher import result shaping so services keep business flow separate from presentation metadata.
public sealed class VoucherImportResultFactory : IVoucherImportResultFactory
{
    private const string ImportType = "voucher";
    private const string DateFormat = "yyyy-MM-dd";

    public VoucherImportResult CreateImportResult(VoucherImportResultCreateRequest request)
    {
        return new VoucherImportResult
        {
            ImportBatchId = request.ImportBatchId,
            TotalRows = request.TotalRows,
            ValidRows = request.ValidRows,
            StagedRows = request.StagedRows,
            VoucherPostingDate = FormatDate(request.PostingDate),
            VoucherReversalDate = FormatDate(request.ReversalDate),
            RowHeaders = request.RowHeaders.ToList(),
            RowResults = request.RowResults.ToList(),
            Errors = request.Errors.ToList()
        };
    }

    public ExcelImportResult CreateEditSessionResult(VoucherEditSessionResultCreateRequest request)
    {
        return new ExcelImportResult
        {
            ImportType = ImportType,
            EditSessionId = request.EditSessionId,
            ImportBatchId = request.ImportBatchId,
            VoucherPostingDate = FormatDate(request.PostingDate),
            VoucherReversalDate = FormatDate(request.ReversalDate),
            TotalRows = request.TotalRows,
            ValidRows = request.ValidRows,
            InvalidRows = request.InvalidRows,
            StagedRows = request.StagedRows,
            Errors = request.Errors.ToList(),
            RowHeaders = request.RowHeaders.ToList(),
            RowResults = request.RowResults.ToList()
        };
    }

    public ExcelImportResult ToExcelImportResult(VoucherImportResult result, string importType)
    {
        return new ExcelImportResult
        {
            ImportType = importType,
            ImportBatchId = result.ImportBatchId,
            TotalRows = result.TotalRows,
            ValidRows = result.ValidRows,
            InvalidRows = result.InvalidRows,
            StagedRows = result.StagedRows,
            VoucherPostingDate = result.VoucherPostingDate,
            VoucherReversalDate = result.VoucherReversalDate,
            RowHeaders = result.RowHeaders.ToList(),
            RowResults = result.RowResults.ToList(),
            Errors = result.Errors.ToList()
        };
    }

    private static string? FormatDate(DateTime? date)
    {
        return date?.ToString(DateFormat);
    }
}

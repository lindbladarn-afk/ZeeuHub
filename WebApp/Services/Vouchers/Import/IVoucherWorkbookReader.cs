using Microsoft.AspNetCore.Http;

namespace WebApp.Services.Vouchers;

// Reads voucher workbook headers and non-empty row data from uploaded files.
public interface IVoucherWorkbookReader
{
    Task<VoucherWorkbookReadResult> ReadAsync(IFormFile file, CancellationToken cancellationToken = default);
}

public sealed class VoucherWorkbookReadResult
{
    public IReadOnlyList<string> RowHeaders { get; init; } = Array.Empty<string>();
    public IReadOnlyList<VoucherWorkbookRow> Rows { get; init; } = Array.Empty<VoucherWorkbookRow>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed class VoucherWorkbookRow
{
    public required int RowNo { get; init; }
    public required Dictionary<string, string> Data { get; init; }
}

using System.Text.Json;
using WebApp.Models.Voucher;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Vouchers;

// Creates staging rows from normalized voucher import data.
public interface IVoucherStagingRowFactory
{
    PortalVoucherStagingRow Create(VoucherStagingRowCreateRequest request);
}

public sealed class VoucherStagingRowCreateRequest
{
    public required Guid ImportBatchId { get; init; }
    public required int RowNo { get; init; }
    public required IReadOnlyDictionary<string, string> RowData { get; init; }
    public required IReadOnlyDictionary<string, string> RawJsonData { get; init; }
    public JsonSerializerOptions? JsonOptions { get; init; }
    public required string ImportedBy { get; init; }
    public ExcelImportUserContext? UserContext { get; init; }
    public DateTime? PostingDate { get; init; }
    public DateTime? ReversalDate { get; init; }
}

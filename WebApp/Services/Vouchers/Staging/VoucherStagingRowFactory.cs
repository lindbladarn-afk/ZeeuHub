using System.Text.Json;
using WebApp.Models.Voucher;

namespace WebApp.Services.Vouchers;

// Maps voucher row dictionaries into the Jeeves staging table contract.
public sealed class VoucherStagingRowFactory : IVoucherStagingRowFactory
{
    public PortalVoucherStagingRow Create(VoucherStagingRowCreateRequest request)
        => new()
        {
            ImportBatchId = request.ImportBatchId,
            RowNo = request.RowNo,
            RawJson = JsonSerializer.Serialize(request.RawJsonData, request.JsonOptions),
            Account = GetValue(request.RowData, "Account"),
            Amount = GetFirstValue(request.RowData, "Debit", "Credit"),
            Ktonr = GetValue(request.RowData, "Account"),
            Koststallekod = GetValue(request.RowData, "Cost center"),
            Kostbar = GetValue(request.RowData, "Cost unit"),
            K4 = GetValue(request.RowData, "K4"),
            K5 = GetValue(request.RowData, "K5"),
            K6 = GetValue(request.RowData, "K6"),
            K7 = GetValue(request.RowData, "K7"),
            Projcode = GetValue(request.RowData, "Project"),
            Debbel = GetValue(request.RowData, "Debit"),
            Krebel = GetValue(request.RowData, "Credit"),
            Momskod = GetValue(request.RowData, "VAT code"),
            VoucherText = GetValue(request.RowData, "Voucher text"),
            Autoregel = GetValue(request.RowData, "Allocation"),
            Vbbelopp = GetFirstValue(request.RowData, "Debit", "Credit"),
            ImportedAt = DateTime.UtcNow,
            ImportedBy = request.ImportedBy,
            CompanyId = request.UserContext?.CompanyId,
            ForetagKod = request.UserContext?.ForetagKod,
            UserId = request.UserContext?.UserId,
            PostingDate = request.PostingDate?.Date,
            AterBokfDat = request.ReversalDate?.Date
        };

    private static string GetValue(IReadOnlyDictionary<string, string> rowData, string header)
    {
        return rowData.TryGetValue(header, out var value) ? value?.Trim() ?? string.Empty : string.Empty;
    }

    private static string GetFirstValue(IReadOnlyDictionary<string, string> rowData, params string[] headers)
    {
        foreach (var header in headers)
        {
            var value = GetValue(rowData, header);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }
}

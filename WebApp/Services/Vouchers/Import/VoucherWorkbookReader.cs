using Microsoft.AspNetCore.Http;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.Vouchers;

// Converts uploaded voucher workbooks into canonical header and row dictionaries.
public sealed class VoucherWorkbookReader : IVoucherWorkbookReader
{
    private readonly IExcelImportWorkbookReader _workbookReader;

    public VoucherWorkbookReader()
        : this(new ExcelImportWorkbookReader())
    {
    }

    public VoucherWorkbookReader(IExcelImportWorkbookReader workbookReader)
    {
        _workbookReader = workbookReader;
    }

    public async Task<VoucherWorkbookReadResult> ReadAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var workbook = await _workbookReader.ReadAsync(
            file,
            VoucherValidation.WorkbookDefinition,
            cancellationToken);

        return new VoucherWorkbookReadResult
        {
            RowHeaders = workbook.RowHeaders,
            Rows = workbook.Rows
                .Select(row => new VoucherWorkbookRow
                {
                    RowNo = row.RowNo,
                    Data = row.Data
                })
                .ToList(),
            Errors = workbook.Errors
        };
    }
}

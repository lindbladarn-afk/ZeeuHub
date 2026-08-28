using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebApp.Services.Vouchers;

namespace WebApp.Services.ExcelImport;

public class VoucherImportHandler : IExcelImportHandler
{
    public string ImportType => "voucher";
    public string DisplayName => "Verifikat (Voucher)";

    private readonly IVoucherImportService _voucherImportService;
    private readonly IVoucherImportResultFactory _resultFactory;

    public VoucherImportHandler(
        IVoucherImportService voucherImportService,
        IVoucherImportResultFactory resultFactory)
    {
        _voucherImportService = voucherImportService;
        _resultFactory = resultFactory;
    }

    public bool CanHandle(string? importType) =>
        string.Equals(importType?.Trim(), ImportType, StringComparison.OrdinalIgnoreCase);

    public async Task<ExcelImportResult> ImportAsync(IFormFile file, string importedBy, CancellationToken cancellationToken = default)
    {
        var res = await _voucherImportService.ImportAsync(file, importedBy, postingDate: null, reversalDate: null, cancellationToken);
        return _resultFactory.ToExcelImportResult(res, ImportType);
    }
}

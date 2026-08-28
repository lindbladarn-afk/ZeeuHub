using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebApp.Services.PurchasePrice;

namespace WebApp.Services.ExcelImport;

public class PurchasePriceImportHandler : IExcelImportHandler
{
    public string ImportType => "purchaseprice";
    public string DisplayName => "Inköpspriser";

    private readonly IPurchasePriceImportService _service;

    public PurchasePriceImportHandler(IPurchasePriceImportService service)
    {
        _service = service;
    }

    public bool CanHandle(string? importType) =>
        string.Equals(importType?.Trim(), ImportType, StringComparison.OrdinalIgnoreCase);

    public Task<ExcelImportResult> ImportAsync(IFormFile file, string importedBy, CancellationToken cancellationToken = default)
        => _service.ImportAsync(file, importedBy, cancellationToken);
}

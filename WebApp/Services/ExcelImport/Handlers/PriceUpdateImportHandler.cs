using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebApp.Services.PriceUpdate;

namespace WebApp.Services.ExcelImport;

public class PriceUpdateImportHandler : IExcelImportHandler
{
    public string ImportType => "priceupdate";
    public string DisplayName => "Prisuppdatering";

    private readonly IPriceUpdateImportService _service;

    public PriceUpdateImportHandler(IPriceUpdateImportService service)
    {
        _service = service;
    }

    public bool CanHandle(string? importType) =>
        string.Equals(importType?.Trim(), ImportType, StringComparison.OrdinalIgnoreCase);

    public Task<ExcelImportResult> ImportAsync(IFormFile file, string importedBy, CancellationToken cancellationToken = default) =>
        _service.ImportAsync(file, importedBy, cancellationToken);
}

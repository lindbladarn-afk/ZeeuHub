using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebApp.Services.TransAutoPrice;

namespace WebApp.Services.ExcelImport;

// Routes Trans Auto price uploads to the supplier price-list import pipeline.
public sealed class TransAutoPriceImportHandler : IExcelImportHandler
{
    private readonly ITransAutoPriceImportService _service;

    public TransAutoPriceImportHandler(ITransAutoPriceImportService service)
    {
        _service = service;
    }

    public string ImportType => "transautoprice";
    public string DisplayName => "Prisinläsning Trans Auto";

    public bool CanHandle(string? importType) =>
        string.Equals(importType?.Trim(), ImportType, StringComparison.OrdinalIgnoreCase);

    public Task<ExcelImportResult> ImportAsync(
        IFormFile file,
        string importedBy,
        CancellationToken cancellationToken = default) =>
        _service.ImportAsync(file, importedBy, cancellationToken);
}

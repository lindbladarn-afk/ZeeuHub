using Microsoft.AspNetCore.Http;
using WebApp.Services.PressKogyoPrice;

namespace WebApp.Services.ExcelImport;

// Routes Press Kogyo price uploads to the supplier price-list import pipeline.
public sealed class PressKogyoPriceImportHandler : IExcelImportHandler
{
    private readonly IPressKogyoPriceImportService _service;

    public PressKogyoPriceImportHandler(IPressKogyoPriceImportService service)
    {
        _service = service;
    }

    public string ImportType => "presskogyoprice";
    public string DisplayName => "Prisinläsning Press Kogyo";

    public bool CanHandle(string? importType) =>
        string.Equals(importType?.Trim(), ImportType, StringComparison.OrdinalIgnoreCase);

    public Task<ExcelImportResult> ImportAsync(
        IFormFile file,
        string importedBy,
        CancellationToken cancellationToken = default) =>
        _service.ImportAsync(file, importedBy, cancellationToken);
}

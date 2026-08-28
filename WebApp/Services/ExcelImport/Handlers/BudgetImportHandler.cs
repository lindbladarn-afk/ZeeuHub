using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WebApp.Services.ExcelImport;

public class BudgetImportHandler : IExcelImportHandler
{
    public string ImportType => "budget";
    public string DisplayName => "Budget";

    private readonly WebApp.Services.Budget.IBudgetImportService _service;

    public BudgetImportHandler(WebApp.Services.Budget.IBudgetImportService service)
    {
        _service = service;
    }

    public bool CanHandle(string? importType) =>
        string.Equals(importType?.Trim(), ImportType, StringComparison.OrdinalIgnoreCase);

    public Task<ExcelImportResult> ImportAsync(IFormFile file, string importedBy, CancellationToken cancellationToken = default)
        => _service.ImportAsync(file, importedBy, cancellationToken);
}

using Microsoft.AspNetCore.Http;
using WebApp.Services.ExcelImport;

namespace WebApp.Services.SupplierPrice;

// Defines the shared import contract used by supplier-price templates and edit sessions.
public interface ISupplierPriceImportService
{
    Task<ExcelImportResult> ImportAsync(
        IFormFile file,
        string importedBy,
        CancellationToken cancellationToken = default);
}
